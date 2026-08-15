//  s&box MCP Server toolset, part two : what the editor believes, and making it notice
//  a change on disk. See SboxMcpServer.cs for the header, licence and the engine-truth
//  half of this same partial class.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Sandbox;

namespace Editor.Mcp;

public static partial class SboxMcpServer
{
	public enum CompilerSlot
	{
		Both,
		Game,
		Editor,
	}


	/// <summary>Report which project the editor has open, where it sits on disk, and the compiler settings currently live in memory. Start here when an on-disk change is not taking effect: the settings reported are what Roslyn is actually using, which is not necessarily what the .sbproj on disk now says.</summary>
	[McpTool.ReadOnly( "project_info" )]
	public static ProjectInfo Info()
	{
		var project = Open();

		return new ProjectInfo
		{
			Ident = project.Config?.FullIdent,
			Title = project.Config?.Title,
			Type = project.Config?.Type,
			RootDirectory = project.RootDirectory?.FullName,
			ConfigFilePath = project.ConfigFilePath,
			Active = project.Active,
			Broken = project.Broken,
			IsPublished = project.IsPublished,
			HasCompiler = project.HasCompiler,
			CompileSettings = LiveCompileSettings( project ),
		};
	}

	/// <summary>List the project's compilers with their build state. A compiler sitting at NeedsBuild true while IsBuilding is false has work queued that nothing has started, which is what a stalled build looks like from the outside.</summary>
	[McpTool.ReadOnly( "project_compilers" )]
	public static CompilerList Compilers(
		[Description( "Which compiler to report on." )] CompilerSlot slot = CompilerSlot.Both )
	{
		var project = Open();

		return new CompilerList
		{
			Compilers = Slots( project, slot ).Select( Describe ).ToArray(),
		};
	}

	/// <summary>Ask each compiler what source changes it has actually noticed since its last build. This is the direct answer to "did my edit register", and it separates a file the compiler never saw from a file it saw and rejected.</summary>
	[McpTool.ReadOnly( "project_source_changes" )]
	public static SourceChangeList SourceChanges(
		[Description( "Which compiler to report on." )] CompilerSlot slot = CompilerSlot.Both )
	{
		var project = Open();

		var compilers = Slots( project, slot ).Select( Noticed ).ToArray();

		return new SourceChangeList
		{
			Compilers = compilers,

			// An empty summary is genuinely ambiguous: the engine also returns {} when there is no
			// previous syntax tree to diff against, which is every compiler on a cold editor.
			Hint = compilers.Length > 0 && compilers.All( compiler => compiler.ChangeCount == 0 )
				? "Every change set is empty. That means either nothing changed, or the compilers have no baseline to diff against yet because they have not built since the editor opened. Run project_build once, then ask again - a second empty answer is a real one."
				: null,
		};
	}

	/// <summary>Return current compile diagnostics as structured rows with file and line, so errors can be read without scraping read_console. Errors sort first.</summary>
	[McpTool.ReadOnly( "project_compile_errors" )]
	public static DiagnosticList CompileErrors(
		[Description( "Include warnings alongside errors." )] bool includeWarnings = false,
		[Description( "Maximum rows to return." )] [Sandbox.Range( 1, 500 )] int limit = 50 )
	{
		var raw = Project.CompileGroup?.BuildResult.Diagnostics;

		var floor = includeWarnings ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error;

		var rows = raw is null
			? Array.Empty<DiagnosticRow>()
			: raw.Where( diagnostic => diagnostic.Severity >= floor )
				.OrderByDescending( diagnostic => diagnostic.Severity )
				.Take( limit )
				.Select( Flatten )
				.ToArray();

		return new DiagnosticList
		{
			Count = rows.Length,
			Diagnostics = rows,
			Hint = rows.Length == 0 ? "No diagnostics. If a source edit still is not live, run project_source_changes, then project_assembly_freshness." : null,
		};
	}

	/// <summary>Compare what each compiler last built against what the process has actually loaded. Recompiling does not always cure a stale assembly: the editor goes on serving the version it loaded, and compile_status reads green the whole time.</summary>
	[McpTool.ReadOnly( "project_assembly_freshness" )]
	public static AssemblyFreshness AssemblyFreshnessOf()
	{
		var project = Open();
		var loaded = AppDomain.CurrentDomain.GetAssemblies();

		var rows = Slots( project, CompilerSlot.Both )
			.Select( slot =>
			{
				var built = slot.Compiler.Output?.Version;

				// Hotloading leaves older copies behind under the same simple name, so the newest
				// one loaded is the one the process is serving
				var copies = loaded
					.Select( assembly => assembly.GetName() )
					.Where( name => Same( name.Name, slot.Compiler.AssemblyName ) )
					.Select( name => name.Version )
					.Where( version => version is not null )
					.ToArray();

				var newest = copies.Length == 0 ? null : copies.Max();
				var stale = built is not null && newest is not null && built > newest;

				return new AssemblyFreshnessRow
				{
					Slot = slot.Label,
					Name = slot.Compiler.Name,
					AssemblyName = slot.Compiler.AssemblyName,
					BuiltVersion = built?.ToString(),
					LoadedVersion = newest?.ToString(),
					LoadedCopies = copies.Length,
					Stale = stale,
					Hint = built is null ? "This compiler has never produced a build, so there is nothing to compare."
						: newest is null ? "Nothing by that assembly name is loaded. The build has not been hotloaded into the process at all."
						: stale ? "The process is running an older build than the compiler produced. Rebuilding will not fix this - close and reopen the editor."
						: null,
				};
			} )
			.ToArray();

		return new AssemblyFreshness
		{
			Assemblies = rows,
			AnyStale = rows.Any( row => row.Stale ),
		};
	}

	/// <summary>Resolve one or more content paths against everything currently mounted, applying the same _c suffix rule the engine applies when it loads a resource. A path that resolves to nothing does not throw at runtime: Model.Load hands back the engine's error model, so a typo in a .item or a prefab compiles clean, passes every headless test and ships an orange world.</summary>
	[McpTool.ReadOnly( "project_content_path" )]
	public static ContentPathResult ContentPath(
		[Description( "One content path, or several separated by commas. For example \"models/citizen/citizen.vmdl\"." )] string paths )
	{
		var wanted = (paths ?? "")
			.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.ToArray();

		if ( wanted.Length == 0 )
			throw new Exception( "Pass at least one content path. Separate several with commas." );

		var rows = wanted.Select( path =>
		{
			// ResourceLibrary.LoadGameResource does exactly this before it touches the filesystem
			var compiled = path.EndsWith( "_c", StringComparison.Ordinal );
			var resolved = compiled ? path : path + "_c";
			var exists = FileSystem.Mounted?.FileExists( resolved ) ?? false;

			return new ContentPathRow
			{
				Input = path,
				Resolved = resolved,
				Exists = exists,
				// The asset system indexes source paths, so it wants the name without the suffix
				Package = exists ? AssetSystem.FindByPath( compiled ? path[..^2] : path )?.Package?.FullIdent : null,
				Hint = exists ? null : "Nothing mounted provides this. Run project_content_search on its parent directory to find the real spelling - mounted packages often disagree with their own CDN manifests about the path prefix.",
			};
		} ).ToArray();

		return new ContentPathResult
		{
			Count = rows.Length,
			Found = rows.Count( row => row.Exists ),
			Missing = rows.Count( row => !row.Exists ),
			Paths = rows,
		};
	}

	/// <summary>List files under a directory in the mounted content filesystem. This is the "then what IS the right path" companion to project_content_path: mounted packages routinely disagree with their own CDN manifests about the path prefix, and the manifest spelling fails at runtime with no symptom at all.</summary>
	[McpTool.ReadOnly( "project_content_search" )]
	public static ContentSearchResult ContentSearch(
		[Description( "Directory to search, for example \"models/citizen\". Use \"/\" for everything." )] string directory = "/",
		[Description( "Filename pattern, for example \"*.vmdl_c\". Compiled content carries the _c suffix." )] string pattern = "*",
		[Description( "Recurse into subdirectories. A recursive search from \"/\" walks every mounted package and is slow." )] bool recursive = true,
		[Description( "Maximum results." )] [Sandbox.Range( 1, 500 )] int limit = 50 )
	{
		var mounted = FileSystem.Mounted
			?? throw new Exception( "Nothing is mounted yet. Wait for the editor to finish loading the project." );

		var found = mounted.FindFile( string.IsNullOrWhiteSpace( directory ) ? "/" : directory, pattern, recursive ).ToArray();
		var page = found.Take( limit ).ToArray();

		return new ContentSearchResult
		{
			Directory = directory,
			Pattern = pattern,
			Count = page.Length,
			Total = found.Length,
			Files = page,
			Hint = found.Length > 0
				? "These are relative to the directory searched. Pass a full path to project_content_path to confirm it resolves."
				: "Nothing matched. Widen the pattern, or search the parent directory - the prefix is what usually differs from what a manifest says.",
		};
	}

	/// <summary>Reconcile the package references written in the .sbproj against what is actually installed in the cloud cache. install_package mounts a package for this session and writes nothing to the project, so a package installed over MCP works perfectly until the editor restarts and then is simply gone.</summary>
	[McpTool.ReadOnly( "project_package_references" )]
	public static PackageReferenceReport PackageReferences()
	{
		var project = Open();
		var referenced = project.Config?.PackageReferences ?? new List<string>();
		var installed = AssetSystem.GetInstalledPackages();

		var rows = referenced.Select( ident => new PackageReferenceRow
		{
			Ident = ident,
			Installed = AssetSystem.IsCloudInstalled( ident ),
			Title = installed.FirstOrDefault( package => Same( package.FullIdent, ident ) )?.Title,
		} ).ToArray();

		var extra = installed
			.Select( package => package.FullIdent )
			.Where( ident => ident is not null && !referenced.Any( reference => Same( reference, ident ) ) )
			.OrderBy( ident => ident, StringComparer.OrdinalIgnoreCase )
			.ToArray();

		var missing = rows.Where( row => !row.Installed ).Select( row => row.Ident ).ToArray();

		return new PackageReferenceReport
		{
			References = rows,
			InstalledNotReferenced = extra,
			Hint = missing.Length > 0
				? $"Referenced but not installed: {string.Join( ", ", missing )}. Anything they provide will resolve to nothing at runtime."
				: extra.Length > 0
					? "Installed but not referenced. These are mounted for this session only - install_package writes nothing to the .sbproj, so they vanish at the next editor start. Add them to PackageReferences and run project_reload_config."
					: null,
		};
	}


	/// <summary>Re-read the project's .sbproj from disk into the live config and recreate its compilers, so an externally edited Metadata.Compiler block actually reaches Roslyn. Nothing watches that file, so without this an on-disk config change silently never takes effect.</summary>
	[McpTool( "project_reload_config" )]
	public static ReloadConfigResult ReloadConfig()
	{
		var project = Open();

		var wasGame = project.Compiler;
		var wasEditor = project.EditorCompiler;

		if ( Engine.CallOwned( project, "LoadMinimal" ) is not true )
			throw new Exception( "Reloading the .sbproj failed, most likely a syntax error in it. Check read_console." );

		// UpdateCompiler early-returns when CompilerHash still matches lastCompilerHash, and that
		// hash covers only compile settings, org, ident, type, the standalone flag and package
		// references. Zeroing it is what stops an edit to anything else being a silent no-op.
		Engine.SetOwned( project, "lastCompilerHash", 0 );
		Engine.CallOwned( project, "UpdateCompiler" );

		var recreated = (project.Compiler is { } game && !ReferenceEquals( wasGame, game ))
			|| (project.EditorCompiler is { } editor && !ReferenceEquals( wasEditor, editor ));

		return new ReloadConfigResult
		{
			Reloaded = true,
			CompilersRecreated = recreated,
			CompileSettings = LiveCompileSettings( project ),
			Hint = recreated
				? "Compilers were recreated, so their file watchers are fresh too. Run project_build to compile against the settings above."
				: project.HasCompiler
					? "The config reloaded but no compiler was recreated. That happens when the project loaded a precompiled assembly instead of building one."
					: "The config reloaded and the project now has no compiler at all. Check Active and the code path in project_info - a config edit can deactivate a project.",
		};
	}

	/// <summary>Drop the cached ProjectSettings and push the reloaded input config back into the engine, so an externally edited Input.config, Platform.config or Collision.config actually takes effect. These are cached on first read and never invalidated, which is a separate trap from the .sbproj one.</summary>
	[McpTool( "project_reload_settings" )]
	public static ReloadSettingsResult ReloadSettings()
	{
		var before = Sandbox.Input.GetActions()?.Count() ?? 0;

		Engine.CallShared( typeof( ProjectSettings ), "ClearCache" );

		// Clearing the cache alone changes nothing here: Input.GetActions reads a static field that
		// only Input.ReadConfig ever assigns, and nothing calls it on a settings reload.
		Engine.CallShared( typeof( Sandbox.Input ), "ReadConfig", new object?[] { ProjectSettings.Input } );

		var after = Sandbox.Input.GetActions()?.Count() ?? 0;

		return new ReloadSettingsResult
		{
			Reloaded = true,
			ActionsBefore = before,
			ActionsAfter = after,
			Hint = "Every other config re-reads lazily on next access. Call project_input_actions to confirm Input.config parsed the way you meant it.",
		};
	}

	/// <summary>Dispose and recreate every compiler, then start a build from the source on disk. Returns immediately.</summary>
	[McpTool( "project_rebuild" )]
	public static RebuildResult Rebuild()
	{
		RecreateCompilers();

		return new RebuildResult
		{
			Rebuilding = true,
			Hint = "Poll compile_status until IsBuilding is false, then call project_compile_errors.",
		};
	}

	/// <summary>Build the project and wait for it to finish, then report success along with any errors. This is the one-shot version of project_rebuild followed by polling: reach for it when the question is simply whether the code compiles.</summary>
	[McpTool( "project_build" )]
	public static async Task<BuildResult> Build(
		[Description( "Recreate compilers first, which also resets stale file watchers." )] bool rebuild = true )
	{
		if ( rebuild )
			RecreateCompilers();

		var building = Engine.CallShared( typeof( Project ), "CompileAsync" ) as Task
			?? throw new Exception( "Project.CompileAsync did not return a Task, engine API changed." );

		await building;

		var succeeded = Engine.Peek( building, "Result" ) is true;

		return new BuildResult
		{
			Success = succeeded,
			Errors = succeeded ? null : CompileErrors(),
			Hint = succeeded ? "Built. If the change still is not live, run project_assembly_freshness." : null,
		};
	}


	static Project Open()
	{
		return Project.Current ?? throw new Exception( "No project is open in the editor." );
	}

	static void RecreateCompilers()
	{
		Engine.CallShared( typeof( Project ), "RebuildCompilers" );
	}

	static (string Label, Compiler Compiler)[] Slots( Project project, CompilerSlot slot )
	{
		var found = new List<(string, Compiler)>();

		if ( slot is CompilerSlot.Both or CompilerSlot.Game && project.Compiler is { } game )
			found.Add( ("Game", game) );

		if ( slot is CompilerSlot.Both or CompilerSlot.Editor && project.EditorCompiler is { } editor )
			found.Add( ("Editor", editor) );

		return found.ToArray();
	}

	static Assembly[] ProjectAssemblies()
	{
		var project = Project.Current;
		if ( project is null ) return Array.Empty<Assembly>();

		var names = Slots( project, CompilerSlot.Both ).Select( slot => slot.Compiler.AssemblyName ).ToArray();

		return AppDomain.CurrentDomain.GetAssemblies()
			.Where( assembly => names.Any( name => Same( assembly.GetName().Name, name ) ) )
			.ToArray();
	}

	static CompileSettingsInfo? LiveCompileSettings( Project project )
	{
		if ( project.Config is null ) return null;

		var settings = Engine.CallOwned( project.Config, "GetCompileSettings" );
		if ( settings is null ) return null;

		return new CompileSettingsInfo
		{
			TreatWarningsAsErrors = Engine.Peek( settings, "TreatWarningsAsErrors" ) as bool?,
			Nullables = Engine.Peek( settings, "Nullables" ) as bool?,
			RootNamespace = Engine.Peek( settings, "RootNamespace" ) as string,
			DefineConstants = Engine.Peek( settings, "DefineConstants" ) as string,
			NoWarn = Engine.Peek( settings, "NoWarn" ) as string,
			WarningsAsErrors = Engine.Peek( settings, "WarningsAsErrors" ) as string,
		};
	}

	static CompilerInfo Describe( (string Label, Compiler Compiler) slot )
	{
		// Compiler.BuildSuccess is Output?.Successful ?? false, which cannot tell a failed build
		// from one that has never run. Output itself can.
		var success = slot.Compiler.Output?.Successful;

		return new CompilerInfo
		{
			Slot = slot.Label,
			Name = slot.Compiler.Name,
			AssemblyName = slot.Compiler.AssemblyName,
			IsBuilding = slot.Compiler.IsBuilding,
			NeedsBuild = slot.Compiler.NeedsBuild,
			Success = success,
			Hint = success is null ? "Never built. This is not a failure, it is an absence - run project_build." : null,
		};
	}

	static SourceChangeInfo Noticed( (string Label, Compiler Compiler) slot )
	{
		var changes = Engine.Hidden( slot.Compiler, "ChangeSummary" ) as Dictionary<string, object>;

		return new SourceChangeInfo
		{
			Slot = slot.Label,
			Name = slot.Compiler.Name,
			ChangeCount = changes?.Count ?? 0,
			Changes = changes,
		};
	}


	static DiagnosticRow Flatten( Diagnostic diagnostic )
	{
		var span = diagnostic.Location.GetLineSpan();
		var located = !string.IsNullOrEmpty( span.Path );

		return new DiagnosticRow
		{
			Id = diagnostic.Id,
			Severity = diagnostic.Severity.ToString(),
			Message = diagnostic.GetMessage(),
			File = located ? span.Path : null,
			Line = located ? span.StartLinePosition.Line + 1 : null,
		};
	}


	public class ProjectInfo
	{
		public string? Ident { get; set; }
		public string? Title { get; set; }
		public string? Type { get; set; }
		public string? RootDirectory { get; set; }
		public string? ConfigFilePath { get; set; }
		public bool Active { get; set; }
		public bool Broken { get; set; }
		public bool IsPublished { get; set; }
		public bool HasCompiler { get; set; }
		public CompileSettingsInfo? CompileSettings { get; set; }
	}

	public class CompileSettingsInfo
	{
		public bool? TreatWarningsAsErrors { get; set; }
		public bool? Nullables { get; set; }
		public string? RootNamespace { get; set; }
		public string? DefineConstants { get; set; }
		public string? NoWarn { get; set; }
		public string? WarningsAsErrors { get; set; }
	}

	public class CompilerList
	{
		public CompilerInfo[] Compilers { get; set; } = Array.Empty<CompilerInfo>();
	}

	public class CompilerInfo
	{
		[Description( "Game or Editor." )]
		public string? Slot { get; set; }
		public string? Name { get; set; }
		public string? AssemblyName { get; set; }
		public bool IsBuilding { get; set; }
		public bool NeedsBuild { get; set; }
		[Description( "Whether the last build succeeded. Null when it has never built." )]
		public bool? Success { get; set; }
		public string? Hint { get; set; }
	}

	public class SourceChangeList
	{
		public SourceChangeInfo[] Compilers { get; set; } = Array.Empty<SourceChangeInfo>();
		public string? Hint { get; set; }
	}

	public class SourceChangeInfo
	{
		public string? Slot { get; set; }
		public string? Name { get; set; }
		public int ChangeCount { get; set; }
		[Description( "The compiler's own change summary. Empty also means no baseline to diff against." )]
		public Dictionary<string, object>? Changes { get; set; }
	}

	public class DiagnosticList
	{
		public int Count { get; set; }
		public DiagnosticRow[] Diagnostics { get; set; } = Array.Empty<DiagnosticRow>();
		public string? Hint { get; set; }
	}

	public class DiagnosticRow
	{
		[Description( "The diagnostic id, like \"CS0246\"." )]
		public string? Id { get; set; }
		public string? Severity { get; set; }
		public string? Message { get; set; }
		public string? File { get; set; }
		public int? Line { get; set; }
	}

	public class AssemblyFreshness
	{
		public AssemblyFreshnessRow[] Assemblies { get; set; } = Array.Empty<AssemblyFreshnessRow>();
		[Description( "True when any assembly the process is serving is older than what was built." )]
		public bool AnyStale { get; set; }
	}

	public class AssemblyFreshnessRow
	{
		public string? Slot { get; set; }
		public string? Name { get; set; }
		public string? AssemblyName { get; set; }
		[Description( "The version the compiler last produced. Null when it has never built." )]
		public string? BuiltVersion { get; set; }
		[Description( "The newest version of that assembly loaded in the process." )]
		public string? LoadedVersion { get; set; }
		[Description( "How many copies are loaded. More than one is normal after hotloads." )]
		public int LoadedCopies { get; set; }
		public bool Stale { get; set; }
		public string? Hint { get; set; }
	}

	public class ContentPathResult
	{
		public int Count { get; set; }
		public int Found { get; set; }
		public int Missing { get; set; }
		public ContentPathRow[] Paths { get; set; } = Array.Empty<ContentPathRow>();
	}

	public class ContentPathRow
	{
		public required string Input { get; set; }
		[Description( "The path with the engine's _c suffix applied, which is what it actually opens." )]
		public string? Resolved { get; set; }
		[Description( "False means this loads the error model at runtime without throwing." )]
		public bool Exists { get; set; }
		[Description( "Which mounted package provides it." )]
		public string? Package { get; set; }
		public string? Hint { get; set; }
	}

	public class ContentSearchResult
	{
		public string? Directory { get; set; }
		public string? Pattern { get; set; }
		public int Count { get; set; }
		[Description( "How many matched before the limit was applied." )]
		public int Total { get; set; }
		[Description( "Paths relative to the directory searched." )]
		public string[] Files { get; set; } = Array.Empty<string>();
		public string? Hint { get; set; }
	}

	public class PackageReferenceReport
	{
		public PackageReferenceRow[] References { get; set; } = Array.Empty<PackageReferenceRow>();
		[Description( "Installed in the cloud cache but absent from the .sbproj, so gone at next editor start." )]
		public string[] InstalledNotReferenced { get; set; } = Array.Empty<string>();
		public string? Hint { get; set; }
	}

	public class PackageReferenceRow
	{
		public required string Ident { get; set; }
		public bool Installed { get; set; }
		public string? Title { get; set; }
	}

	public class ReloadConfigResult
	{
		public bool Reloaded { get; set; }
		[Description( "False means the reload changed nothing Roslyn cares about." )]
		public bool CompilersRecreated { get; set; }
		public CompileSettingsInfo? CompileSettings { get; set; }
		public string? Hint { get; set; }
	}

	public class ReloadSettingsResult
	{
		public bool Reloaded { get; set; }
		public int ActionsBefore { get; set; }
		[Description( "Unchanged after editing Input.config means the file did not parse. Check read_console." )]
		public int ActionsAfter { get; set; }
		public string? Hint { get; set; }
	}

	public class RebuildResult
	{
		public bool Rebuilding { get; set; }
		public string? Hint { get; set; }
	}

	public class BuildResult
	{
		public bool Success { get; set; }
		[Description( "The diagnostics that failed it. Null on success." )]
		public DiagnosticList? Errors { get; set; }
		public string? Hint { get; set; }
	}


	static class Engine
	{
		const BindingFlags Shared = BindingFlags.Static | BindingFlags.NonPublic;
		const BindingFlags Owned = BindingFlags.Instance | BindingFlags.NonPublic;

		public static Type Named( string fullName )
		{
			return typeof( Project ).Assembly.GetType( fullName )
				?? throw new Exception( $"{fullName} not found in Sandbox.Engine, engine API changed." );
		}

		public static object? CallShared( Type owner, string name, object?[]? args = null )
		{
			return Required( owner.GetMethod( name, Shared ), owner, name ).Invoke( null, args );
		}

		public static object? CallOwned( object target, string name, object?[]? args = null )
		{
			var owner = target.GetType();
			return Required( owner.GetMethod( name, Owned ), owner, name ).Invoke( target, args );
		}

		public static object? SharedField( Type owner, string name )
		{
			return Required( owner.GetField( name, Shared ), owner, name ).GetValue( null );
		}

		public static void SetOwned( object target, string name, object? value )
		{
			var owner = target.GetType();
			Required( owner.GetField( name, Owned ), owner, name ).SetValue( target, value );
		}

		public static object? Hidden( object target, string name )
		{
			var owner = target.GetType();
			return Required( owner.GetProperty( name, Owned ), owner, name ).GetValue( target );
		}

		public static object? Peek( object? target, string name )
		{
			return target?.GetType().GetProperty( name )?.GetValue( target );
		}

		public static object? Invoke( object? target, string name, object?[]? args = null )
		{
			return target?.GetType().GetMethod( name )?.Invoke( target, args );
		}

		static T Required<T>( T? member, Type owner, string name ) where T : MemberInfo
		{
			return member ?? throw new Exception( $"{owner.Name}.{name} not found, engine API changed." );
		}
	}
}
