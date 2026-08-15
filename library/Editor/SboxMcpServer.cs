// =============================================================================
//  s&box Skill : s&box MCP Server toolset
//
//  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
//  Links    : https://fobiat.dev/   https://github.com/fobiat
//  Licence  : MIT, see LICENSE at the repository root.
//
//  Eleven MCP tools for the s&box editor, in three groups: ask the running engine
//  what an API really is, ask the editor what it currently believes, and make it
//  notice a change on disk. Drop this file into a project's Editor/ folder and
//  they appear under the "sbox_mcp_server" toolset in list_toolsets.
//
//  The third group exists because three engine behaviours can each swallow an
//  edit without raising anything. The .sbproj is read once at boot and never
//  watched (FN-3). ProjectSettings/*.config is cached on first read and never
//  invalidated. Compiler file watchers stop firing once the compilers are
//  recreated in-process (FN-4). Every one leaves you having edited a file, seen
//  no error, and concluding the edit was wrong when it simply never arrived.
//
//  Almost everything worth reaching here is internal, so it goes through the
//  Engine class at the foot of this file. Editor assemblies are unsandboxed but
//  still sit outside Sandbox.Engine, so reflection is the only route. Verified
//  against engine 26.08.05, and compile-checked by editor-mcp/compilecheck.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Sandbox;

namespace Editor.Mcp;

/// <summary>
/// Project, config and compiler tools for driving an s&amp;box project from outside the editor.
/// </summary>
[McpToolset( "sbox_mcp_server", "Query the running engine for real type signatures and input actions, read project and compiler state including what each compiler has noticed, list compile errors, reload an externally edited .sbproj or ProjectSettings config, and rebuild from source on disk." )]
public static class SboxMcpServer
{
	// ============================================================ ask the engine

	/// <summary>
	/// Search the running engine for a type by name and report what it is. Ask this before
	/// writing an API you are not certain about. The answer comes from the engine actually
	/// loaded in the editor, not from documentation, so it cannot be stale and it cannot be
	/// a plausible invention. No match is a real answer: the type does not exist, so do not
	/// write it.
	/// </summary>
	[McpTool.ReadOnly( "project_find_type" )]
	public static object FindType(
		[Description( "Type name or fragment, case insensitive. For example \"SceneTrace\"." )] string name,
		[Description( "Maximum results. Default 20." )] int limit = 20 )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			throw new Exception( "Pass a type name or fragment to search for." );

		var matches = KnownTypes()
			.Where( type => Matches( type.Name, name ) || Matches( type.FullName, name ) )
			.OrderBy( type => type.Name?.Length ?? int.MaxValue )
			.Take( Cap( limit ) )
			.Select( type => new
			{
				type.Name,
				type.Namespace,
				Kind = Shape( type ),
				BaseType = type.BaseType?.Name,
				Methods = type.Methods?.Length ?? 0,
				Properties = type.Properties?.Length ?? 0,
				type.Description,
			} )
			.ToArray();

		return new
		{
			Count = matches.Length,
			Types = matches,
			Hint = matches.Length > 0
				? "Call project_type_members for the full signature list of one of these."
				: $"Nothing matches \"{name}\" in the loaded engine. Treat that as proof it does not exist rather than as a search that needs rewording.",
		};
	}

	/// <summary>
	/// List a type's methods and properties with real signatures, read from the running
	/// engine. This is the ground truth an API reference is only an approximation of, so
	/// prefer it whenever the two might disagree, and always for a type you are about to
	/// call something unfamiliar on.
	/// </summary>
	[McpTool.ReadOnly( "project_type_members" )]
	public static object TypeMembers(
		[Description( "Exact type name, for example \"SceneTrace\"." )] string type,
		[Description( "Only members whose name contains this. Optional." )] string? filter = null,
		[Description( "Maximum members of each kind. Default 60." )] int limit = 60 )
	{
		var found = KnownTypes().FirstOrDefault( candidate => Same( candidate.Name, type ) )
			?? throw new Exception( $"No type named \"{type}\" in the loaded engine. Run project_find_type first." );

		bool Wanted( string memberName ) => string.IsNullOrWhiteSpace( filter ) || Matches( memberName, filter );

		return new
		{
			Type = found.FullName,
			Kind = Shape( found ),
			BaseType = found.BaseType?.FullName,

			Methods = (found.Methods ?? Array.Empty<MethodDescription>())
				.Where( method => !method.IsSpecialName && Wanted( method.Name ) )
				.Take( Cap( limit ) )
				.Select( method => new { method.Name, Signature = Signature( method ), method.Description } )
				.ToArray(),

			Properties = (found.Properties ?? Array.Empty<PropertyDescription>())
				.Where( property => Wanted( property.Name ) )
				.Take( Cap( limit ) )
				.Select( property => new
				{
					property.Name,
					Type = Readable( property.PropertyType ),
					Access = property.CanRead && property.CanWrite ? "get set" : property.CanRead ? "get" : "set",
					property.Description,
				} )
				.ToArray(),
		};
	}

	/// <summary>
	/// List the input actions this project defines, with their keyboard and gamepad bindings.
	/// Input actions are strings resolved at runtime, so Input.Down( "jump" ) on an action that
	/// does not exist compiles cleanly and silently never fires. Check the name here first.
	/// </summary>
	[McpTool.ReadOnly( "project_input_actions" )]
	public static object InputActions()
	{
		var actions = Sandbox.Input.GetActions()?.ToArray() ?? Array.Empty<Sandbox.InputAction>();

		return new
		{
			Count = actions.Length,
			Actions = actions.Select( action => new
			{
				action.Name,
				action.Title,
				Group = action.GroupName,
				Keyboard = action.KeyboardCode,
				Gamepad = action.GamepadCode.ToString(),
			} ).ToArray(),
		};
	}

	// ============================================================ ask the editor

	/// <summary>
	/// Report which project the editor has open, where it sits on disk, and the compiler
	/// settings currently live in memory. Start here when an on-disk change is not taking
	/// effect: the settings reported are what Roslyn is actually using, which is not
	/// necessarily what the .sbproj on disk now says.
	/// </summary>
	[McpTool.ReadOnly( "project_info" )]
	public static object Info()
	{
		var project = Open();

		return new
		{
			Ident = project.Config?.FullIdent,
			Title = project.Config?.Title,
			Type = project.Config?.Type,
			RootDirectory = project.RootDirectory?.FullName,
			ConfigFilePath = project.ConfigFilePath,
			project.Active,
			project.Broken,
			project.IsPublished,
			project.HasCompiler,
			CompileSettings = LiveCompileSettings( project ),
		};
	}

	/// <summary>
	/// List the project's compilers with their build state. A compiler sitting at NeedsBuild
	/// true while IsBuilding is false has work queued that nothing has started, which is what
	/// a stalled build looks like from the outside.
	/// </summary>
	[McpTool.ReadOnly( "project_compilers" )]
	public static object Compilers()
	{
		var project = Open();

		return new
		{
			Compilers = CompilerSlots
				.Select( slot => Describe( project, slot ) )
				.Where( description => description is not null )
				.ToArray(),
		};
	}

	/// <summary>
	/// Ask each compiler what source changes it has actually noticed since its last build.
	/// This is the direct answer to "did my edit register", and it separates a file the
	/// compiler never saw from a file it saw and rejected. An empty summary right after you
	/// edited a .cs file means the watchers are stale: run project_build.
	/// </summary>
	[McpTool.ReadOnly( "project_source_changes" )]
	public static object SourceChanges()
	{
		var project = Open();

		return new
		{
			Compilers = CompilerSlots
				.Select( slot => Noticed( project, slot ) )
				.Where( noticed => noticed is not null )
				.ToArray(),

			Hint = "An empty change set straight after editing a .cs file means the file watchers are stale. Run project_build.",
		};
	}

	/// <summary>
	/// Return current compile diagnostics as structured rows with file and line, so errors can
	/// be read without scraping read_console. Errors sort first. Pass includeWarnings when the
	/// project builds with TreatWarningsAsErrors, because then a warning is what failed it.
	/// </summary>
	[McpTool.ReadOnly( "project_compile_errors" )]
	public static object CompileErrors(
		[Description( "Include warnings alongside errors. Default false." )] bool includeWarnings = false,
		[Description( "Maximum rows to return. Default 50." )] int limit = 50 )
	{
		var raw = Engine.CallShared( typeof( Project ), "GetCompileDiagnostics" ) as IEnumerable;

		var rows = raw is null
			? Array.Empty<object>()
			: raw.Cast<object>()
				.Select( Flatten )
				.Where( row => includeWarnings || row.Severity == "Error" )
				.OrderBy( row => row.Severity == "Error" ? 0 : 1 )
				.Take( Cap( limit ) )
				.ToArray<object>();

		return new
		{
			Count = rows.Length,
			Diagnostics = rows,
			Hint = rows.Length == 0 ? "No diagnostics. If a source edit still is not live, run project_source_changes." : null,
		};
	}

	// ============================================================ change something

	/// <summary>
	/// Re-read the project's .sbproj from disk into the live config and recreate its compilers,
	/// so an externally edited Metadata.Compiler block actually reaches Roslyn. Nothing watches
	/// that file, so without this an on-disk config change silently never takes effect. Returns
	/// the compiler settings now live, read back from the reloaded config so you can confirm
	/// the change landed rather than assuming it did.
	/// </summary>
	[McpTool( "project_reload_config" )]
	public static object ReloadConfig()
	{
		var project = Open();

		if ( Engine.CallOwned( project, "LoadMinimal" ) is not true )
			throw new Exception( "Reloading the .sbproj failed, most likely a syntax error in it. Check read_console." );

		Engine.CallOwned( project, "UpdateCompiler" );

		return new { Reloaded = true, CompileSettings = LiveCompileSettings( project ) };
	}

	/// <summary>
	/// Drop the cached ProjectSettings so the next read pulls Input.config, Platform.config,
	/// Collision.config and the rest fresh from disk. These are cached on first read and never
	/// invalidated, which is a separate trap from the .sbproj one: edit Input.config externally
	/// and the old actions keep resolving until the editor restarts. Returns the reloaded
	/// action count so you can confirm the file parsed.
	/// </summary>
	[McpTool( "project_reload_settings" )]
	public static object ReloadSettings()
	{
		Engine.CallShared( typeof( ProjectSettings ), "ClearCache" );

		return new
		{
			Reloaded = true,
			InputActions = Sandbox.Input.GetActions()?.Count() ?? 0,
			Hint = "Configs re-read lazily on next access. Call project_input_actions to confirm Input.config parsed as expected.",
		};
	}

	/// <summary>
	/// Dispose and recreate every compiler, then start a build from the source on disk. Returns
	/// immediately. Recreating the compilers is what resets stale file watchers, so this is the
	/// fix when edits made outside the editor are not being picked up. Prefer project_build
	/// unless you specifically want to carry on working while it runs.
	/// </summary>
	[McpTool( "project_rebuild" )]
	public static object Rebuild()
	{
		RecreateCompilers();

		return new
		{
			Rebuilding = true,
			Hint = "Poll compile_status until IsBuilding is false, then call project_compile_errors.",
		};
	}

	/// <summary>
	/// Build the project and wait for it to finish, then report success along with any errors.
	/// This is the one-shot version of project_rebuild followed by polling: reach for it when
	/// the question is simply whether the code compiles.
	/// </summary>
	[McpTool( "project_build" )]
	public static async Task<object> Build(
		[Description( "Recreate compilers first, which also resets stale file watchers. Default true." )] bool rebuild = true )
	{
		if ( rebuild )
			RecreateCompilers();

		var building = Engine.CallShared( typeof( Project ), "CompileAsync" ) as Task
			?? throw new Exception( "Project.CompileAsync did not return a Task, engine API changed." );

		await building;

		var succeeded = Engine.Peek( building, "Result" ) is true;

		return new { Success = succeeded, Errors = succeeded ? null : CompileErrors() };
	}

	// ============================================================ project plumbing

	static readonly string[] CompilerSlots = { "Compiler", "EditorCompiler" };

	static Project Open()
	{
		return Project.Current ?? throw new Exception( "No project is open in the editor." );
	}

	static void RecreateCompilers()
	{
		Engine.CallShared( typeof( Project ), "RebuildCompilers" );
	}

	/// <summary>
	/// The compiler settings Roslyn is using right now, which is the whole point: they can
	/// differ from the .sbproj on disk, and that difference is usually the bug.
	/// </summary>
	static object? LiveCompileSettings( Project project )
	{
		if ( project.Config is null ) return null;

		var settings = Engine.CallOwned( project.Config, "GetCompileSettings" );
		if ( settings is null ) return null;

		return new
		{
			TreatWarningsAsErrors = Engine.Peek( settings, "TreatWarningsAsErrors" ),
			Nullables = Engine.Peek( settings, "Nullables" ),
			RootNamespace = Engine.Peek( settings, "RootNamespace" ),
			DefineConstants = Engine.Peek( settings, "DefineConstants" ),
			NoWarn = Engine.Peek( settings, "NoWarn" ),
			WarningsAsErrors = Engine.Peek( settings, "WarningsAsErrors" ),
		};
	}

	static object? Describe( Project project, string slot )
	{
		var compiler = Engine.Hidden( project, slot );
		if ( compiler is null ) return null;

		return new
		{
			Slot = slot,
			Name = Engine.Peek( compiler, "Name" ),
			AssemblyName = Engine.Peek( compiler, "AssemblyName" ),
			IsBuilding = Engine.Peek( compiler, "IsBuilding" ),
			NeedsBuild = Engine.Peek( compiler, "NeedsBuild" ),
			BuildSuccess = Engine.Peek( compiler, "BuildSuccess" ),
		};
	}

	static object? Noticed( Project project, string slot )
	{
		var compiler = Engine.Hidden( project, slot );
		if ( compiler is null ) return null;

		return new
		{
			Slot = slot,
			Name = Engine.Peek( compiler, "Name" ),
			Changes = Engine.Hidden( compiler, "ChangeSummary" ),
		};
	}

	// ============================================================ type plumbing

	/// <summary>
	/// The editor's type library rather than the game's, because the game one is only
	/// populated while a scene is loaded and these tools have to answer at author time.
	/// </summary>
	static IEnumerable<TypeDescription> KnownTypes()
	{
		var library = Sandbox.Internal.GlobalToolsNamespace.EditorTypeLibrary
			?? throw new Exception( "EditorTypeLibrary is not available yet. Wait for the editor to finish loading." );

		return library.GetTypes<object>() ?? Enumerable.Empty<TypeDescription>();
	}

	static string Shape( TypeDescription type )
	{
		if ( type.IsEnum ) return "enum";
		if ( type.IsInterface ) return "interface";
		if ( type.IsStatic ) return "static class";
		if ( type.IsValueType ) return "struct";
		if ( type.IsAbstract ) return "abstract class";
		return "class";
	}

	static string Signature( MethodDescription method )
	{
		var arguments = method.Parameters.Select( p => $"{Readable( p.ParameterType )} {p.Name}" );
		return $"{Readable( method.ReturnType )} {method.Name}( {string.Join( ", ", arguments )} )";
	}

	/// <summary>
	/// Render a generic type the way a person writes it, so List`1 reads as List&lt;Entity&gt;.
	/// </summary>
	static string Readable( Type? type )
	{
		if ( type is null ) return "void";
		if ( !type.IsGenericType ) return type.Name;

		var arguments = type.GetGenericArguments().Select( Readable );
		return $"{type.Name.Split( '`' )[0]}<{string.Join( ", ", arguments )}>";
	}

	// ============================================================ diagnostics

	/// <summary>
	/// Flatten a Roslyn Diagnostic without referencing Microsoft.CodeAnalysis, which editor
	/// addon code cannot assume is available to it.
	/// </summary>
	static Diagnostic Flatten( object diagnostic )
	{
		var span = Engine.Invoke( Engine.Peek( diagnostic, "Location" ), "GetLineSpan" );
		var line = Engine.Peek( Engine.Peek( span, "StartLinePosition" ), "Line" ) as int?;

		return new Diagnostic
		{
			Id = Engine.Peek( diagnostic, "Id" ) as string,
			Severity = Engine.Peek( diagnostic, "Severity" )?.ToString(),
			Message = diagnostic.ToString(),
			File = Engine.Peek( span, "Path" ) as string,
			Line = line + 1,
		};
	}

	sealed class Diagnostic
	{
		public string? Id { get; set; }
		public string? Severity { get; set; }
		public string? Message { get; set; }
		public string? File { get; set; }
		public int? Line { get; set; }
	}

	// ============================================================ small helpers

	static int Cap( int limit ) => Math.Max( 1, limit );

	static bool Matches( string? haystack, string? needle ) =>
		needle is not null && haystack?.Contains( needle, StringComparison.OrdinalIgnoreCase ) == true;

	static bool Same( string? a, string? b ) => string.Equals( a, b, StringComparison.OrdinalIgnoreCase );

	/// <summary>
	/// The reflection layer. Editor assemblies are unsandboxed but sit outside Sandbox.Engine,
	/// so everything internal has to come through here.
	///
	/// A lookup that should exist and does not throws the name it wanted. That is deliberate:
	/// the natural failure of a file like this is silent staleness after an engine update,
	/// where a renamed member turns a tool into a no-op that still reports success. A thrown
	/// name is uglier and tells you exactly which member to go and read.
	/// </summary>
	static class Engine
	{
		const BindingFlags Shared = BindingFlags.Static | BindingFlags.NonPublic;
		const BindingFlags Owned = BindingFlags.Instance | BindingFlags.NonPublic;

		/// <summary>Call an internal static method, or throw naming it.</summary>
		public static object? CallShared( Type owner, string name )
		{
			return Required( owner.GetMethod( name, Shared ), owner, name ).Invoke( null, null );
		}

		/// <summary>Call an internal instance method on a target, or throw naming it.</summary>
		public static object? CallOwned( object target, string name )
		{
			var owner = target.GetType();
			return Required( owner.GetMethod( name, Owned ), owner, name ).Invoke( target, null );
		}

		/// <summary>Read an internal instance property, or throw naming it.</summary>
		public static object? Hidden( object target, string name )
		{
			var owner = target.GetType();
			return Required( owner.GetProperty( name, Owned ), owner, name ).GetValue( target );
		}

		/// <summary>Read a public property that is allowed to be absent, yielding null instead.</summary>
		public static object? Peek( object? target, string name )
		{
			return target?.GetType().GetProperty( name )?.GetValue( target );
		}

		/// <summary>Call a public method that is allowed to be absent, yielding null instead.</summary>
		public static object? Invoke( object? target, string name )
		{
			return target?.GetType().GetMethod( name )?.Invoke( target, null );
		}

		static T Required<T>( T? member, Type owner, string name ) where T : MemberInfo
		{
			return member ?? throw new Exception( $"{owner.Name}.{name} not found, engine API changed." );
		}
	}
}
