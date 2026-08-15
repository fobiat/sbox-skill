//  s&box MCP Server toolset : eighteen MCP tools for the s&box editor.
//
//  Ask the running engine what an API really is, ask the editor what it currently
//  believes, and make it notice a change on disk. Drop this file (and its
//  SboxMcpServer.Editor.cs partner) into a project's Editor/ folder; it registers
//  as "sbox_mcp_server".
//
//  Why each tool exists, which engine behaviours swallow an edit silently, and every
//  internal member reached by reflection: editor-mcp/README.md.
//
//  fobiat (Kyle Tarff) <kyle@fobiat.dev>  https://github.com/fobiat/sbox-skill
//  MIT, see LICENSE. Engine 26.08.05, compile-checked by editor-mcp/compilecheck.

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

/// <summary>Project, config, content and compiler tools for driving an s&amp;box project from outside the editor.</summary>
[McpToolset( "sbox_mcp_server", "Query the running engine for real type signatures, members, enum values, input actions and console commands, resolve content paths before they silently load the error model, read project, package and compiler state including assembly staleness, list compile errors, reload an externally edited .sbproj or ProjectSettings config, and rebuild from source on disk." )]
public static partial class SboxMcpServer
{
	public enum MemberKind
	{
		All,
		Methods,
		Properties,
		Fields,
	}


	/// <summary>Search the running engine for a type by name and report what it is. Ask this before writing an API you are not certain about.</summary>
	[McpTool.ReadOnly( "project_find_type" )]
	public static TypeSearch FindType(
		[Description( "Type name or fragment, case insensitive. For example \"SceneTrace\"." )] string name,
		[Description( "Maximum results." )] [Sandbox.Range( 1, 500 )] int limit = 20 )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			throw new Exception( "Pass a type name or fragment to search for." );

		var matches = KnownTypes()
			.Where( type => Matches( type.Name, name ) || Matches( type.FullName, name ) )
			.OrderBy( type => type.Name?.Length ?? int.MaxValue )
			.Take( limit )
			.Select( type => new FoundType
			{
				Name = type.Name,
				Namespace = type.Namespace,
				Kind = Shape( type ),
				BaseType = type.BaseType?.Name,
				Methods = type.Methods?.Length ?? 0,
				Properties = type.Properties?.Length ?? 0,
				Description = type.Description,
			} )
			.ToArray();

		return new TypeSearch
		{
			Count = matches.Length,
			Types = matches,
			Hint = matches.Length > 0
				? "Call project_type_members for the full signature list of one of these, or project_enum_values for an enum."
				: $"Nothing matches \"{name}\" in the loaded engine. Treat that as proof it does not exist rather than as a search that needs rewording.",
		};
	}

	/// <summary>List a type's methods and properties with real signatures, read from the running engine, and mark anything carrying [Obsolete]. This is the ground truth an API reference is only an approximation of, so prefer it whenever the two might disagree, and always for a type you are about to call something unfamiliar on.</summary>
	[McpTool.ReadOnly( "project_type_members" )]
	public static TypeMembers TypeMembersOf(
		[Description( "Exact type name, for example \"SceneTrace\"." )] string type,
		[Description( "Only members whose name contains this. Optional." )] string? filter = null,
		[Description( "Restrict the listing to one kind of member." )] MemberKind kind = MemberKind.All,
		[Description( "Maximum members of each kind." )] [Sandbox.Range( 1, 500 )] int limit = 60 )
	{
		var found = Resolve( type );

		bool Wanted( string memberName ) => string.IsNullOrWhiteSpace( filter ) || Matches( memberName, filter );

		var wantMethods = kind is MemberKind.All or MemberKind.Methods;
		var wantProperties = kind is MemberKind.All or MemberKind.Properties;

		return new TypeMembers
		{
			Type = found.FullName,
			Kind = Shape( found ),
			BaseType = found.BaseType?.FullName,

			Methods = !wantMethods ? Array.Empty<MethodRow>() : (found.Methods ?? Array.Empty<MethodDescription>())
				.Where( method => !method.IsSpecialName && Wanted( method.Name ) )
				.Take( limit )
				.Select( method => new MethodRow
				{
					Name = method.Name,
					Signature = Signature( method ),
					Static = method.IsStatic,
					Obsolete = Deprecation( method ),
					Description = method.Description,
				} )
				.ToArray(),

			Properties = !wantProperties ? Array.Empty<PropertyRow>() : (found.Properties ?? Array.Empty<PropertyDescription>())
				.Where( property => Wanted( property.Name ) )
				.Take( limit )
				.Select( property => new PropertyRow
				{
					Name = property.Name,
					Type = Readable( property.PropertyType ),
					Access = property.CanRead && property.CanWrite ? "get set" : property.CanRead ? "get" : "set",
					Static = property.IsStatic,
					Obsolete = Deprecation( property ),
					Description = property.Description,
				} )
				.ToArray(),

			Hint = found.IsEnum
				? "This is an enum, so it has no methods or properties worth listing. Call project_enum_values for its named values."
				: null,
		};
	}

	/// <summary>Search every loaded type for members whose name contains a fragment, and report which type each one is declared on. Reach for this when you know roughly what a method is called but not what it hangs off, which is the case project_type_members cannot answer because it needs the exact type name up front.</summary>
	[McpTool.ReadOnly( "project_find_member" )]
	public static MemberSearch FindMember(
		[Description( "Member name or fragment, case insensitive. For example \"RunTrace\"." )] string name,
		[Description( "Restrict the search to one kind of member." )] MemberKind kind = MemberKind.All,
		[Description( "Only members declared on types whose name contains this. Optional." )] string? type = null,
		[Description( "Maximum results." )] [Sandbox.Range( 1, 500 )] int limit = 30 )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			throw new Exception( "Pass a member name or fragment to search for." );

		bool WantedKind( MemberDescription member ) => kind switch
		{
			MemberKind.Methods => member.IsMethod,
			MemberKind.Properties => member.IsProperty,
			MemberKind.Fields => member.IsField,
			_ => member.IsMethod || member.IsProperty || member.IsField,
		};

		var matches = KnownTypes()
			.Where( owner => string.IsNullOrWhiteSpace( type ) || Matches( owner.Name, type ) )
			// DeclaredMembers, not Members: an inherited member would otherwise repeat once per subclass
			.SelectMany( owner => (owner.DeclaredMembers ?? Array.Empty<MemberDescription>())
				.Where( member => WantedKind( member ) && Matches( member.Name, name ) )
				.Select( member => new FoundMember
				{
					Type = owner.FullName,
					Name = member.Name,
					Kind = member.IsMethod ? "method" : member.IsProperty ? "property" : "field",
					Signature = member is MethodDescription method ? Signature( method ) : null,
					Static = member.IsStatic,
					Obsolete = Deprecation( member ),
					Description = member.Description,
				} ) )
			.OrderBy( member => member.Name.Length )
			.Take( limit )
			.ToArray();

		return new MemberSearch
		{
			Count = matches.Length,
			Members = matches,
			Hint = matches.Length > 0
				? "Call project_type_members on one of these types for its full signature list."
				: $"No member anywhere in the loaded engine contains \"{name}\". Treat that as proof it does not exist.",
		};
	}

	/// <summary>List an enum's named values with their numeric values. Enums have no methods or properties, so project_type_members reports one as empty, which reads as "the type does not exist" when it means "wrong tool".</summary>
	[McpTool.ReadOnly( "project_enum_values" )]
	public static EnumValues EnumValuesOf(
		[Description( "Exact enum name, for example \"HitboxTags\"." )] string type )
	{
		var found = Resolve( type );

		if ( !found.IsEnum )
			throw new Exception( $"\"{found.FullName}\" is a {Shape( found )}, not an enum. Call project_type_members for it instead." );

		var description = EditorTypeLibrary.GetEnumDescription( found.TargetType )
			?? throw new Exception( $"The engine has no enum description for \"{found.FullName}\", which should not happen. Check read_console." );

		var entries = description.Select( entry => new EnumEntry
		{
			Name = entry.Name,
			Value = entry.IntegerValue,
			Title = entry.Title,
			Group = entry.Group,
			Description = entry.Description,
		} ).ToArray();

		return new EnumValues
		{
			Type = found.FullName,
			Count = entries.Length,
			Values = entries,
		};
	}

	/// <summary>List the input actions this project defines, with their keyboard and gamepad bindings. Input actions are strings resolved at runtime, so Input.Down( "jump" ) on an action that does not exist compiles cleanly and silently never fires.</summary>
	[McpTool.ReadOnly( "project_input_actions" )]
	public static InputActionList InputActions()
	{
		var actions = Sandbox.Input.GetActions()?.ToArray() ?? Array.Empty<Sandbox.InputAction>();

		return new InputActionList
		{
			Count = actions.Length,
			Actions = actions.Select( action => new InputActionRow
			{
				Name = action.Name,
				Title = action.Title,
				Group = action.GroupName,
				Keyboard = action.KeyboardCode,
				Gamepad = action.GamepadCode.ToString(),
			} ).ToArray(),
		};
	}

	/// <summary>List the console commands and convars the engine currently knows, optionally only the ones this project's own assemblies registered. A console command given the wrong argument form prints its usage and changes nothing, which from the outside is indistinguishable from having run, so check the real name and shape here before driving anything through console_command.</summary>
	[McpTool.ReadOnly( "project_console_commands" )]
	public static ConsoleCommandList ConsoleCommands(
		[Description( "Only commands whose name contains this, case insensitive. Optional." )] string? filter = null,
		[Description( "Only commands registered by this project's own assemblies." )] bool projectOnly = false,
		[Description( "Maximum results." )] [Sandbox.Range( 1, 500 )] int limit = 50 )
	{
		var members = Engine.SharedField( Engine.Named( "Sandbox.ConVarSystem" ), "Members" ) as IDictionary
			?? throw new Exception( "ConVarSystem.Members is not a dictionary any more, engine API changed." );

		var owned = ProjectAssemblies();

		bool FromProject( object command ) =>
			owned.Any( assembly => Engine.Invoke( command, "IsFromAssembly", new object?[] { assembly } ) is true );

		var rows = new List<ConsoleCommandRow>();

		foreach ( var command in members.Values )
		{
			if ( command is null ) continue;

			var name = Engine.Peek( command, "Name" ) as string;
			if ( name is null || !(string.IsNullOrWhiteSpace( filter ) || Matches( name, filter )) ) continue;

			var mine = FromProject( command );
			if ( projectOnly && !mine ) continue;

			rows.Add( new ConsoleCommandRow
			{
				Name = name,
				Kind = Engine.Peek( command, "IsConCommand" ) is true ? "command" : "convar",
				Help = Engine.Peek( command, "Help" ) as string,
				Usage = Engine.Invoke( command, "BuildDescription" ) as string,
				IsAdmin = Engine.Peek( command, "IsAdmin" ) is true,
				IsServer = Engine.Peek( command, "IsServer" ) is true,
				IsCheat = Engine.Peek( command, "IsCheat" ) is true,
				FromProject = mine,
			} );
		}

		var page = rows.OrderBy( row => row.Name, StringComparer.OrdinalIgnoreCase ).Take( limit ).ToArray();

		return new ConsoleCommandList
		{
			Count = page.Length,
			Total = rows.Count,
			Commands = page,
			Hint = page.Length == 0 && projectOnly
				? "Nothing here came from this project. Either its assemblies have not loaded yet or nothing carries [ConVar]."
				: null,
		};
	}


	static IEnumerable<TypeDescription> KnownTypes()
	{
		var library = Sandbox.Internal.GlobalToolsNamespace.EditorTypeLibrary
			?? throw new Exception( "EditorTypeLibrary is not available yet. Wait for the editor to finish loading." );

		return library.GetTypes<object>() ?? Enumerable.Empty<TypeDescription>();
	}

	/// <summary>Resolve a type by simple or full name, preferring a top-level match and naming the alternatives rather than silently answering about the wrong type.</summary>
	static TypeDescription Resolve( string type )
	{
		var matches = KnownTypes()
			.Where( candidate => Same( candidate.Name, type ) || Same( candidate.FullName, type ) )
			.ToArray();

		if ( matches.Length == 0 )
			throw new Exception( $"No type named \"{type}\" in the loaded engine. Run project_find_type first." );

		if ( matches.Length == 1 )
			return matches[0];

		var exact = matches.Where( candidate => Same( candidate.FullName, type ) ).ToArray();
		if ( exact.Length == 1 ) return exact[0];

		// A nested type's FullName carries a '+'. Asking for "SyncFlags" means Sandbox.SyncFlags,
		// not Terrain's nested one, and picking the first match answered about the wrong type.
		var topLevel = matches.Where( candidate => candidate.FullName?.Contains( '+' ) != true ).ToArray();
		if ( topLevel.Length == 1 ) return topLevel[0];

		var names = string.Join( ", ", matches.Select( candidate => candidate.FullName ).Take( 6 ) );
		throw new Exception( $"\"{type}\" is ambiguous across {matches.Length} types: {names}. Pass a full name." );
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

	static string Readable( Type? type )
	{
		if ( type is null ) return "void";
		if ( !type.IsGenericType ) return type.Name;

		var arguments = type.GetGenericArguments().Select( Readable );
		return $"{type.Name.Split( '`' )[0]}<{string.Join( ", ", arguments )}>";
	}

	static string? Deprecation( MemberDescription member )
	{
		var obsolete = member.GetCustomAttribute<ObsoleteAttribute>();
		if ( obsolete is null ) return null;

		return string.IsNullOrWhiteSpace( obsolete.Message ) ? "obsolete" : obsolete.Message;
	}


	static bool Matches( string? haystack, string? needle ) =>
		needle is not null && haystack?.Contains( needle, StringComparison.OrdinalIgnoreCase ) == true;

	static bool Same( string? a, string? b ) => string.Equals( a, b, StringComparison.OrdinalIgnoreCase );


	public class TypeSearch
	{
		public int Count { get; set; }
		public FoundType[] Types { get; set; } = Array.Empty<FoundType>();
		public string? Hint { get; set; }
	}

	public class FoundType
	{
		public string? Name { get; set; }
		public string? Namespace { get; set; }
		[Description( "class, struct, interface, enum, abstract class or static class." )]
		public string? Kind { get; set; }
		public string? BaseType { get; set; }
		public int Methods { get; set; }
		public int Properties { get; set; }
		public string? Description { get; set; }
	}

	public class TypeMembers
	{
		public string? Type { get; set; }
		public string? Kind { get; set; }
		public string? BaseType { get; set; }
		public MethodRow[] Methods { get; set; } = Array.Empty<MethodRow>();
		public PropertyRow[] Properties { get; set; } = Array.Empty<PropertyRow>();
		public string? Hint { get; set; }
	}

	public class MethodRow
	{
		public required string Name { get; set; }
		[Description( "The signature as C# would write it, return type first." )]
		public string? Signature { get; set; }
		public bool Static { get; set; }
		[Description( "The [Obsolete] message when the member is deprecated, otherwise null. Do not write new code against it." )]
		public string? Obsolete { get; set; }
		public string? Description { get; set; }
	}

	public class PropertyRow
	{
		public required string Name { get; set; }
		public string? Type { get; set; }
		[Description( "\"get\", \"set\" or \"get set\"." )]
		public string? Access { get; set; }
		public bool Static { get; set; }
		[Description( "The [Obsolete] message when the member is deprecated, otherwise null. Do not write new code against it." )]
		public string? Obsolete { get; set; }
		public string? Description { get; set; }
	}

	public class MemberSearch
	{
		public int Count { get; set; }
		public FoundMember[] Members { get; set; } = Array.Empty<FoundMember>();
		public string? Hint { get; set; }
	}

	public class FoundMember
	{
		[Description( "The type declaring this member. Pass it to project_type_members." )]
		public string? Type { get; set; }
		public required string Name { get; set; }
		[Description( "method, property or field." )]
		public string? Kind { get; set; }
		public string? Signature { get; set; }
		public bool Static { get; set; }
		public string? Obsolete { get; set; }
		public string? Description { get; set; }
	}

	public class EnumValues
	{
		public string? Type { get; set; }
		public int Count { get; set; }
		public EnumEntry[] Values { get; set; } = Array.Empty<EnumEntry>();
		public string? Hint { get; set; }
	}

	public class EnumEntry
	{
		[Description( "The name to write in code." )]
		public required string Name { get; set; }
		public long Value { get; set; }
		public string? Title { get; set; }
		public string? Group { get; set; }
		public string? Description { get; set; }
	}

	public class InputActionList
	{
		public int Count { get; set; }
		public InputActionRow[] Actions { get; set; } = Array.Empty<InputActionRow>();
	}

	public class InputActionRow
	{
		[Description( "The exact string Input.Down and friends expect." )]
		public required string Name { get; set; }
		public string? Title { get; set; }
		public string? Group { get; set; }
		public string? Keyboard { get; set; }
		public string? Gamepad { get; set; }
	}

	public class ConsoleCommandList
	{
		public int Count { get; set; }
		[Description( "How many matched before the limit was applied." )]
		public int Total { get; set; }
		public ConsoleCommandRow[] Commands { get; set; } = Array.Empty<ConsoleCommandRow>();
		public string? Hint { get; set; }
	}

	public class ConsoleCommandRow
	{
		public required string Name { get; set; }
		[Description( "command or convar. A convar takes a value, a command takes arguments." )]
		public string? Kind { get; set; }
		public string? Help { get; set; }
		[Description( "For a convar, its current and default value alongside the help text." )]
		public string? Usage { get; set; }
		public bool IsAdmin { get; set; }
		public bool IsServer { get; set; }
		public bool IsCheat { get; set; }
		[Description( "Registered by this project's own code rather than by the engine." )]
		public bool FromProject { get; set; }
	}
}
