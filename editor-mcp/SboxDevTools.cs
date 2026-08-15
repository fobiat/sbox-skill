// =============================================================================
//  s&box Skill : sbox_dev MCP toolset
//
//  Author   : Kyle (fobiat) <kyle@fobiat.dev>
//  Links    : https://fobiat.dev/   https://github.com/fobiat
//  Licence  : MIT, see LICENSE at the repository root.
//
//  Eleven editor MCP tools in three groups: query the running engine for what an
//  API really is, read what the editor currently believes, and make it notice a
//  change on disk. Drop this file into a project's Editor/ folder and the tools
//  appear under the "sbox_dev" toolset in list_toolsets.
//
//  The gap is real and costs whole sessions, because it has three separate
//  causes and none of them produce an error:
//
//    1. The .sbproj is read at editor boot and written from the Project Settings
//       page. Nothing watches it. An edited Metadata.Compiler block never
//       reaches Roslyn.                                        (field note FN-3)
//    2. ProjectSettings/*.config files are cached on first read. An edited
//       Input.config or Platform.config keeps serving the old values.
//    3. After compilers are recreated in-process, their source file watchers
//       have been observed to stop firing, so .cs edits never compile.
//                                                              (field note FN-4)
//
//  In each case an agent edits a file, sees no error, and concludes its change
//  was wrong. These tools let it check instead of guess.
//
//  Most engine members used here are internal, so they are reached by
//  reflection: editor assemblies are unsandboxed but still sit outside
//  Sandbox.Engine. Everything is verified against engine 26.08.05. Reflected
//  members resolve through Required*, which throws the missing name, because
//  the failure mode of a file like this is silent staleness after an engine
//  update and a thrown name beats a tool that quietly returns success.
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
[McpToolset( "sbox_dev", "Query the running engine for real type signatures and input actions, read project and compiler state including what each compiler has noticed, list compile errors, reload an externally edited .sbproj or ProjectSettings config, and rebuild from source on disk." )]
public static class SboxDevTools
{
	const BindingFlags StaticInternal = BindingFlags.Static | BindingFlags.NonPublic;
	const BindingFlags InstanceInternal = BindingFlags.Instance | BindingFlags.NonPublic;

	// ---------------------------------------------------------------- reading

	/// <summary>
	/// Report which project the editor has open, where it sits on disk, and the compiler
	/// settings currently live in memory. Start here when an on-disk change is not taking
	/// effect: the settings reported are what Roslyn is actually using, which is not
	/// necessarily what the .sbproj on disk now says.
	/// </summary>
	[McpTool.ReadOnly( "project_info" )]
	public static object ProjectInfo()
	{
		var project = CurrentProject();

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
			CompileSettings = ReadCompileSettings( project ),
		};
	}

	/// <summary>
	/// List the project's compilers with their build state. A compiler sitting at NeedsBuild
	/// true while IsBuilding is false has work queued that nothing has started, which is what
	/// a stalled build looks like from the outside.
	/// </summary>
	[McpTool.ReadOnly( "project_compilers" )]
	public static object ProjectCompilers()
	{
		var project = CurrentProject();

		return new
		{
			Compilers = new[] { ReadCompiler( project, "Compiler" ), ReadCompiler( project, "EditorCompiler" ) }
				.Where( x => x is not null )
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
	public static object ProjectSourceChanges()
	{
		var project = CurrentProject();

		object? Summary( string slot )
		{
			var compiler = ReflectedProperty( typeof( Project ), slot, InstanceInternal )?.GetValue( project );
			if ( compiler is null ) return null;

			var summary = ReflectedProperty( compiler.GetType(), "ChangeSummary", InstanceInternal )?.GetValue( compiler );

			return new
			{
				Slot = slot,
				Name = ReadMember( compiler, "Name" ),
				Changes = summary,
			};
		}

		return new
		{
			Compilers = new[] { Summary( "Compiler" ), Summary( "EditorCompiler" ) }.Where( x => x is not null ).ToArray(),
			Hint = "An empty change set straight after editing a .cs file means the file watchers are stale. Run project_build.",
		};
	}

	/// <summary>
	/// Return current compile diagnostics as structured rows with file and line, so errors can
	/// be read without scraping read_console. Errors sort first. Pass includeWarnings when the
	/// project builds with TreatWarningsAsErrors, because then a warning is what failed it.
	/// </summary>
	[McpTool.ReadOnly( "project_compile_errors" )]
	public static object ProjectCompileErrors(
		[Description( "Include warnings alongside errors. Default false." )] bool includeWarnings = false,
		[Description( "Maximum rows to return. Default 50." )] int limit = 50 )
	{
		var raw = RequiredMethod( typeof( Project ), "GetCompileDiagnostics", StaticInternal )
			.Invoke( null, null ) as IEnumerable;

		var rows = raw is null
			? Array.Empty<object>()
			: raw.Cast<object>()
				.Select( ReadDiagnostic )
				.Where( d => includeWarnings || d.Severity == "Error" )
				.OrderBy( d => d.Severity == "Error" ? 0 : 1 )
				.Take( Math.Max( 1, limit ) )
				.ToArray<object>();

		return new
		{
			Count = rows.Length,
			Diagnostics = rows,
			Hint = rows.Length == 0 ? "No diagnostics. If a source edit still is not live, run project_source_changes." : null,
		};
	}

	/// <summary>
	/// List the input actions this project defines, with their keyboard and gamepad bindings.
	/// Input actions are strings resolved at runtime, so Input.Down( "jump" ) on an action that
	/// does not exist compiles cleanly and silently never fires. Check the name here first.
	/// </summary>
	[McpTool.ReadOnly( "project_input_actions" )]
	public static object ProjectInputActions()
	{
		var actions = Sandbox.Input.GetActions()?.ToArray() ?? Array.Empty<Sandbox.InputAction>();

		return new
		{
			Count = actions.Length,
			Actions = actions.Select( a => new
			{
				a.Name,
				a.Title,
				Group = a.GroupName,
				Keyboard = a.KeyboardCode,
				Gamepad = a.GamepadCode.ToString(),
			} ).ToArray(),
		};
	}

	/// <summary>
	/// Search the running engine for a type by name and report what it is. Ask this before
	/// writing an API you are not certain about. The answer comes from the engine actually
	/// loaded in the editor, not from documentation, so it cannot be stale and it cannot be
	/// a plausible invention. No match is a real answer: the type does not exist, so do not
	/// write it.
	/// </summary>
	[McpTool.ReadOnly( "project_find_type" )]
	public static object ProjectFindType(
		[Description( "Type name or fragment, case insensitive. For example \"SceneTrace\"." )] string name,
		[Description( "Maximum results. Default 20." )] int limit = 20 )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			throw new Exception( "Pass a type name or fragment to search for." );

		var matches = AllTypes()
			.Where( t => Contains( t.Name, name ) || Contains( t.FullName, name ) )
			.OrderBy( t => t.Name?.Length ?? int.MaxValue )
			.Take( Math.Max( 1, limit ) )
			.Select( t => new
			{
				t.Name,
				t.Namespace,
				Kind = TypeKind( t ),
				BaseType = t.BaseType?.Name,
				Methods = t.Methods?.Length ?? 0,
				Properties = t.Properties?.Length ?? 0,
				t.Description,
			} )
			.ToArray();

		return new
		{
			Count = matches.Length,
			Types = matches,
			Hint = matches.Length == 0
				? $"Nothing matches \"{name}\" in the loaded engine. Treat that as proof it does not exist rather than as a search that needs rewording."
				: "Call project_type_members for the full signature list of one of these.",
		};
	}

	/// <summary>
	/// List a type's methods and properties with real signatures, read from the running
	/// engine. This is the ground truth an API reference is only an approximation of, so
	/// prefer it whenever the two might disagree, and always for a type you are about to
	/// call something unfamiliar on.
	/// </summary>
	[McpTool.ReadOnly( "project_type_members" )]
	public static object ProjectTypeMembers(
		[Description( "Exact type name, for example \"SceneTrace\"." )] string type,
		[Description( "Only members whose name contains this. Optional." )] string? filter = null,
		[Description( "Maximum members of each kind. Default 60." )] int limit = 60 )
	{
		var target = AllTypes().FirstOrDefault( t => string.Equals( t.Name, type, StringComparison.OrdinalIgnoreCase ) )
			?? throw new Exception( $"No type named \"{type}\" in the loaded engine. Run project_find_type first." );

		bool Wanted( string memberName ) => string.IsNullOrWhiteSpace( filter ) || Contains( memberName, filter );
		var cap = Math.Max( 1, limit );

		var methods = (target.Methods ?? Array.Empty<MethodDescription>())
			.Where( m => !m.IsSpecialName && Wanted( m.Name ) )
			.Take( cap )
			.Select( m => new { m.Name, Signature = FormatMethod( m ), m.Description } )
			.ToArray();

		var properties = (target.Properties ?? Array.Empty<PropertyDescription>())
			.Where( p => Wanted( p.Name ) )
			.Take( cap )
			.Select( p => new
			{
				p.Name,
				Type = FriendlyName( p.PropertyType ),
				Access = p.CanRead && p.CanWrite ? "get set" : p.CanRead ? "get" : "set",
				p.Description,
			} )
			.ToArray();

		return new
		{
			Type = target.FullName,
			Kind = TypeKind( target ),
			BaseType = target.BaseType?.FullName,
			Methods = methods,
			Properties = properties,
		};
	}

	// ---------------------------------------------------------------- writing

	/// <summary>
	/// Re-read the project's .sbproj from disk into the live config and recreate its compilers,
	/// so an externally edited Metadata.Compiler block actually reaches Roslyn. Nothing watches
	/// that file, so without this an on-disk config change silently never takes effect. Returns
	/// the compiler settings now live, read back from the reloaded config so you can confirm
	/// the change landed rather than assuming it did.
	/// </summary>
	[McpTool( "project_reload_config" )]
	public static object ProjectReloadConfig()
	{
		var project = CurrentProject();

		var loaded = RequiredMethod( typeof( Project ), "LoadMinimal", InstanceInternal )
			.Invoke( project, null ) is true;

		if ( !loaded )
			throw new Exception( "Reloading the .sbproj failed, most likely a syntax error in it. Check read_console." );

		RequiredMethod( typeof( Project ), "UpdateCompiler", InstanceInternal ).Invoke( project, null );

		return new { Reloaded = true, CompileSettings = ReadCompileSettings( project ) };
	}

	/// <summary>
	/// Drop the cached ProjectSettings so the next read pulls Input.config, Platform.config,
	/// Collision.config and the rest fresh from disk. These are cached on first read and never
	/// invalidated, which is a separate trap from the .sbproj one: edit Input.config externally
	/// and the old actions keep resolving until the editor restarts. Returns the reloaded
	/// action count so you can confirm the file parsed.
	/// </summary>
	[McpTool( "project_reload_settings" )]
	public static object ProjectReloadSettings()
	{
		RequiredMethod( typeof( Sandbox.ProjectSettings ), "ClearCache", StaticInternal ).Invoke( null, null );

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
	public static object ProjectRebuild()
	{
		RequiredMethod( typeof( Project ), "RebuildCompilers", StaticInternal ).Invoke( null, null );

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
	public static async Task<object> ProjectBuild(
		[Description( "Recreate compilers first, which also resets stale file watchers. Default true." )] bool rebuild = true )
	{
		if ( rebuild )
			RequiredMethod( typeof( Project ), "RebuildCompilers", StaticInternal ).Invoke( null, null );

		var task = RequiredMethod( typeof( Project ), "CompileAsync", StaticInternal ).Invoke( null, null ) as Task
			?? throw new Exception( "Project.CompileAsync did not return a Task, engine API changed." );

		await task;

		var success = task.GetType().GetProperty( "Result" )?.GetValue( task ) is true;

		return new { Success = success, Errors = success ? null : ProjectCompileErrors() };
	}

	// ---------------------------------------------------------------- helpers

	static Project CurrentProject()
	{
		return Project.Current ?? throw new Exception( "No project is open in the editor." );
	}

	static bool Contains( string haystack, string needle )
	{
		return haystack?.Contains( needle, StringComparison.OrdinalIgnoreCase ) == true;
	}

	/// <summary>
	/// The editor's type library rather than the game's, because the game one is only
	/// populated while a scene is loaded and these tools have to answer at author time.
	/// </summary>
	static IEnumerable<TypeDescription> AllTypes()
	{
		var library = Sandbox.Internal.GlobalToolsNamespace.EditorTypeLibrary
			?? throw new Exception( "EditorTypeLibrary is not available yet. Wait for the editor to finish loading." );

		return library.GetTypes<object>() ?? Enumerable.Empty<TypeDescription>();
	}

	static string TypeKind( TypeDescription type )
	{
		if ( type.IsEnum ) return "enum";
		if ( type.IsInterface ) return "interface";
		if ( type.IsStatic ) return "static class";
		if ( type.IsValueType ) return "struct";
		if ( type.IsAbstract ) return "abstract class";
		return "class";
	}

	static string FormatMethod( MethodDescription method )
	{
		var args = string.Join( ", ", method.Parameters.Select( p => $"{FriendlyName( p.ParameterType )} {p.Name}" ) );
		return $"{FriendlyName( method.ReturnType )} {method.Name}( {args} )";
	}

	/// <summary>
	/// Render a generic type the way a person writes it, so List`1 reads as List&lt;Entity&gt;.
	/// </summary>
	static string FriendlyName( Type? type )
	{
		if ( type is null ) return "void";
		if ( !type.IsGenericType ) return type.Name;

		var args = string.Join( ", ", type.GetGenericArguments().Select( FriendlyName ) );
		return $"{type.Name.Split( '`' )[0]}<{args}>";
	}

	static object? ReadMember( object? target, string name )
	{
		return target?.GetType().GetProperty( name )?.GetValue( target );
	}

	static object? ReadCompileSettings( Project project )
	{
		if ( project.Config is null ) return null;

		var settings = RequiredMethod( project.Config.GetType(), "GetCompileSettings", InstanceInternal )
			.Invoke( project.Config, null );

		if ( settings is null ) return null;

		return new
		{
			TreatWarningsAsErrors = ReadMember( settings, "TreatWarningsAsErrors" ),
			Nullables = ReadMember( settings, "Nullables" ),
			RootNamespace = ReadMember( settings, "RootNamespace" ),
			DefineConstants = ReadMember( settings, "DefineConstants" ),
			NoWarn = ReadMember( settings, "NoWarn" ),
			WarningsAsErrors = ReadMember( settings, "WarningsAsErrors" ),
		};
	}

	static object? ReadCompiler( Project project, string propertyName )
	{
		var compiler = RequiredProperty( typeof( Project ), propertyName, InstanceInternal ).GetValue( project );
		if ( compiler is null ) return null;

		return new
		{
			Slot = propertyName,
			Name = ReadMember( compiler, "Name" ),
			AssemblyName = ReadMember( compiler, "AssemblyName" ),
			IsBuilding = ReadMember( compiler, "IsBuilding" ),
			NeedsBuild = ReadMember( compiler, "NeedsBuild" ),
			BuildSuccess = ReadMember( compiler, "BuildSuccess" ),
		};
	}

	/// <summary>
	/// Flatten a Roslyn Diagnostic without referencing Microsoft.CodeAnalysis, which editor
	/// addon code cannot assume is available to it.
	/// </summary>
	static DiagnosticRow ReadDiagnostic( object diagnostic )
	{
		var location = ReadMember( diagnostic, "Location" );
		var span = location?.GetType().GetMethod( "GetLineSpan" )?.Invoke( location, null );
		var start = ReadMember( span, "StartLinePosition" );
		var line = ReadMember( start, "Line" ) as int?;

		return new DiagnosticRow
		{
			Id = ReadMember( diagnostic, "Id" ) as string,
			Severity = ReadMember( diagnostic, "Severity" )?.ToString(),
			Message = diagnostic.ToString(),
			File = ReadMember( span, "Path" ) as string,
			Line = line is null ? null : line + 1,
		};
	}

	class DiagnosticRow
	{
		public string? Id { get; set; }
		public string? Severity { get; set; }
		public string? Message { get; set; }
		public string? File { get; set; }
		public int? Line { get; set; }
	}

	static MethodInfo RequiredMethod( Type type, string name, BindingFlags flags )
	{
		return type.GetMethod( name, flags )
			?? throw new Exception( $"{type.Name}.{name} not found, engine API changed." );
	}

	static PropertyInfo RequiredProperty( Type type, string name, BindingFlags flags )
	{
		return type.GetProperty( name, flags )
			?? throw new Exception( $"{type.Name}.{name} not found, engine API changed." );
	}

	/// <summary>
	/// A reflected member that is allowed to be absent, for optional diagnostics that should
	/// degrade to null rather than fail the whole call.
	/// </summary>
	static PropertyInfo? ReflectedProperty( Type type, string name, BindingFlags flags )
	{
		return type.GetProperty( name, flags );
	}
}
