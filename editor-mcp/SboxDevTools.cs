// =============================================================================
//  s&box Skill : sbox_dev MCP toolset
//
//  Author   : Kyle (fobiat) <kyle@fobiat.dev>
//  Links    : https://fobiat.dev/   https://github.com/fobiat
//  Licence  : MIT, see LICENSE at the repository root.
//
//  Editor-side MCP tools that close the gap between editing s&box source on disk
//  and getting it compiled. Drop this file in your project's Editor/ folder and
//  the tools appear under the "sbox_dev" toolset in list_toolsets.
//
//  The gap is real and costs sessions. The engine reads the .sbproj at editor
//  boot, or writes it from the in-editor Project Settings page, and nothing
//  watches it for external edits (field note FN-3). Separately, after compilers
//  are recreated in-process their source file watchers have been observed to stop
//  firing (field note FN-4). Either one leaves an agent editing files that never
//  reach Roslyn, with no error to notice.
//
//  Everything here reaches internal engine API by reflection, because editor
//  assemblies are unsandboxed but still outside Sandbox.Engine. Verified against
//  engine 26.08.05. Each reflected member is resolved through Required*, which
//  throws the missing name: the failure mode of this file is silent staleness
//  after an engine update, and a thrown name beats that.
// =============================================================================

using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Editor.Mcp;

/// <summary>
/// Project and compiler tools for working on an s&amp;box game from outside the editor.
/// </summary>
[McpToolset( "sbox_dev", "Inspect and drive the open project's compilers: read project info, list compile errors, reload an externally edited .sbproj, and rebuild from source on disk." )]
public static class SboxDevTools
{
	const BindingFlags StaticInternal = BindingFlags.Static | BindingFlags.NonPublic;
	const BindingFlags InstanceInternal = BindingFlags.Instance | BindingFlags.NonPublic;

	/// <summary>
	/// Report which project the editor has open, where it lives on disk, and the compiler
	/// settings currently live in memory. Start here when a change on disk is not taking
	/// effect, because the settings reported are the ones Roslyn is actually using, not
	/// whatever the .sbproj on disk currently says.
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
	/// List every compiler the project owns with its current build state. Useful when a build
	/// looks stuck: a compiler sitting at NeedsBuild true with IsBuilding false has work queued
	/// that nothing has started.
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
	/// Return the current compile diagnostics as structured rows rather than console text,
	/// so errors can be read without scraping read_console. Errors come first. Pass
	/// includeWarnings to see warnings too, which matters when the project builds with
	/// TreatWarningsAsErrors, because then a warning is what failed the build.
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
			Hint = rows.Length == 0 ? "No diagnostics. If a source edit still is not live, try project_rebuild." : null,
		};
	}

	/// <summary>
	/// Re-read the project's .sbproj from disk into the live config and recreate its compilers,
	/// so an externally edited Metadata.Compiler block actually reaches Roslyn. Nothing watches
	/// the .sbproj for external edits, so without this an on-disk config change silently never
	/// takes effect. Returns the compiler settings now live, read back from the reloaded config.
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
	/// Dispose and recreate every compiler, then start a full build from the source on disk.
	/// Returns immediately. Use this when source edits made outside the editor are not being
	/// picked up, then poll compile_status, or call project_build instead to wait for the result.
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
	/// This is the one-shot version of project_rebuild followed by polling: prefer it when you
	/// just want to know whether the code compiles.
	/// </summary>
	[McpTool( "project_build" )]
	public static async Task<object> ProjectBuild(
		[Description( "Recreate compilers before building, which also resets stale file watchers. Default true." )] bool rebuild = true )
	{
		if ( rebuild )
			RequiredMethod( typeof( Project ), "RebuildCompilers", StaticInternal ).Invoke( null, null );

		var task = RequiredMethod( typeof( Project ), "CompileAsync", StaticInternal )
			.Invoke( null, null ) as Task
			?? throw new Exception( "Project.CompileAsync did not return a Task, engine API changed." );

		await task;

		var success = task.GetType().GetProperty( "Result" )?.GetValue( task ) is true;

		return new
		{
			Success = success,
			Errors = success ? null : ProjectCompileErrors(),
		};
	}

	static Project CurrentProject()
	{
		return Project.Current ?? throw new Exception( "No project is open in the editor." );
	}

	static object ReadCompileSettings( Project project )
	{
		if ( project.Config is null ) return null;

		var settings = RequiredMethod( project.Config.GetType(), "GetCompileSettings", InstanceInternal )
			.Invoke( project.Config, null );

		if ( settings is null ) return null;

		object Read( string name ) => settings.GetType().GetProperty( name )?.GetValue( settings );

		return new
		{
			TreatWarningsAsErrors = Read( "TreatWarningsAsErrors" ),
			Nullables = Read( "Nullables" ),
			RootNamespace = Read( "RootNamespace" ),
			DefineConstants = Read( "DefineConstants" ),
			NoWarn = Read( "NoWarn" ),
			WarningsAsErrors = Read( "WarningsAsErrors" ),
		};
	}

	static object ReadCompiler( Project project, string propertyName )
	{
		var compiler = RequiredProperty( typeof( Project ), propertyName, InstanceInternal ).GetValue( project );
		if ( compiler is null ) return null;

		object Read( string name ) => compiler.GetType().GetProperty( name )?.GetValue( compiler );

		return new
		{
			Slot = propertyName,
			Name = Read( "Name" ),
			AssemblyName = Read( "AssemblyName" ),
			IsBuilding = Read( "IsBuilding" ),
			NeedsBuild = Read( "NeedsBuild" ),
			BuildSuccess = Read( "BuildSuccess" ),
		};
	}

	/// <summary>
	/// Flatten a Roslyn Diagnostic without referencing Microsoft.CodeAnalysis, which editor
	/// addon code cannot assume is available to it.
	/// </summary>
	static DiagnosticRow ReadDiagnostic( object diagnostic )
	{
		object Read( string name ) => diagnostic.GetType().GetProperty( name )?.GetValue( diagnostic );

		var location = Read( "Location" );
		var span = location?.GetType().GetMethod( "GetLineSpan" )?.Invoke( location, null );
		var path = span?.GetType().GetProperty( "Path" )?.GetValue( span ) as string;

		var start = span?.GetType().GetProperty( "StartLinePosition" )?.GetValue( span );
		var line = start?.GetType().GetProperty( "Line" )?.GetValue( start ) as int?;

		return new DiagnosticRow
		{
			Id = Read( "Id" ) as string,
			Severity = Read( "Severity" )?.ToString(),
			Message = diagnostic.ToString(),
			File = path,
			Line = line is null ? null : line + 1,
		};
	}

	class DiagnosticRow
	{
		public string Id { get; set; }
		public string Severity { get; set; }
		public string Message { get; set; }
		public string File { get; set; }
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
}
