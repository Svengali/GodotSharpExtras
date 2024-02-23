using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;

namespace Godot.Sharp.Extras
{
	public static class Tools
	{
		private static TextInfo _textInfo = new CultureInfo( "en-us", false ).TextInfo;

		public static void ReadyType<T>( T node )
			where T : Node
		{
			node.OnReady<T>();
		}

		/// <summary>
		/// Processes all Attributes for NodePaths.
		/// </summary>
		/// <remarks>
		/// This will fill in fields and register signals as per attributes such as <see cref="NodePathAttribute"/> and <see cref="SignalAttribute"/>.
		/// </remarks>
		/// <param name="node">The node.</param>
		public static void OnReady<T>( this T node )
			where T : Node
		{
			var type = typeof(T);
			

			if( TypeMembers.TryGetValue( type, out var members ) == false )
			{
				var bindingFlags = BindingFlags.DeclaredOnly |

					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
				members = type.GetFields( bindingFlags ).Select( fi => new MemberInfo( fi ) )
							.Concat( type.GetProperties( bindingFlags ).Select( pi => new MemberInfo( pi ) ) )
							.ToArray();
				TypeMembers[type] = members;
			}

			foreach( var member in members )
			{
				GD.Print( $"* Processing {member.Name}" );
				try
				{
					/*
					bool resolveNode = member.CustomAttributes.Count( att => att.GetType() == typeof( ResolveNodeAttribute ) ) > 0;
					bool nodePath    = member.CustomAttributes.Count( att => att.GetType() == typeof( NodePathAttribute    ) ) > 0;
					bool resource    = member.CustomAttributes.Count( att => att.GetType() == typeof( ResourceAttribute    ) ) > 0;
					bool singleton   = member.CustomAttributes.Count( att => att.GetType() == typeof( SingletonAttribute   ) ) > 0;
					*/

					var wasHandled = false;
					foreach( var attr in member.CustomAttributes )
					{
						switch( attr )
						{
							case ResolveNodeAttribute resolveAttr:
								ResolveNodeFromPath( node, member, resolveAttr.TargetFieldName );
								wasHandled = true;
								break;
							case NodePathAttribute pathAttr:
								AssignPathToMember( node, member, pathAttr.NodePath );
								wasHandled = true;
								break;
							case ResourceAttribute resAttr:
								LoadResource( node, member, resAttr.ResourcePath );
								wasHandled = true;
								break;
							case SingletonAttribute singAttr:
								LoadSingleton( node, member, singAttr.Name );
								wasHandled = true;
								break;
						}
					}

					if( !wasHandled )
					{
						//GD.Print( $"Member {member.Name} wasnt handled, using types" );
						var memberType = member.MemberType;

						var isResource = memberType.IsSubclassOf( typeof( Resource ) );
						var isNode = memberType.IsSubclassOf( typeof( Node ) );

						if( isNode )
						{
							GD.Print( $"AssignPathToMember {node.GetPath()}/{member.Name}" );

							AssignPathToMember( node, member, member.Name );

							wasHandled = true;
						}
						else if( isResource )
						{

							/*
							//var nodeName = member.Name;

							//GD.Print( $"NodeName {nodeName}" );

							//var resNameFromNode = nodeName.Replace( "_", "/" );
							//var fullResName = $"res:/{resNameFromNode}";
							//var curPath = node.GetPath();

							//GD.Print( $"NodePath {curPath.GetName(0)}" );

							//var fullPath = curPath.GetName( 0 );
							*/

							/*
							for( int i = 0; i < curPath.GetNameCount() - 1; ++i )
							{
								var curName = curPath.GetName( i );
								fullPath += $"/{curName}";
							}
							*/

							//GD.Print( $"Scene File Path {node?.SceneFilePath}" );


							/*
							var path = node.GetPath();


							GD.Print( $"LoadingResource {path}/{member.Name} {prefixString} {prefix}" );
							*/




							string sceneFile = node.SceneFilePath;
							
							var endOfResPathIndex = sceneFile.LastIndexOf('/');
							
							var path = sceneFile.Substring( 0, endOfResPathIndex );

							var prefix = member.CustomAttributes?.FirstOrDefault( a => a?.GetType() == typeof( PrefixAttribute ) ) as PrefixAttribute;

							var prefixString = prefix?.Prefix;

							path += $"/{prefixString}";


							/*
							for( int i = 1; i < path.GetNameCount(); ++i )
							{
								fullPath += $"/{path.GetName( i )}";
							}
							*/

							var fullName = $"{path}/{member.Name}.tscn";

							GD.Print( $"Loading Resource {"type"} from {fullName}" );

							LoadResource( node, member, fullName );

							wasHandled = true;
						}
					}

					if( !wasHandled )
					{
						GD.PrintErr( $"Member {member.Name} couldnt be handled" );
					}
				}
				catch( Exception ex )
				{
						GD.PrintErr( $"Member {member.Name} got {ex.Message}" );
				}
			}

			if( SignalHandlers.TryGetValue( type, out var handlers ) == false )
			{
				handlers = type.GetMethods( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static )
							.SelectMany( mi => mi.GetCustomAttributes()
								.OfType<SignalHandlerAttribute>()
								.Select( attr => new SignalHandlerInfo( mi.Name, attr ) )
							)
							.ToArray();
				SignalHandlers[type] = handlers;
			}

			foreach( var handler in handlers )
			{
				ConnectSignalHandler( node, handler.MethodName, handler.Attribute );
			}
		}

		private static void ConnectSignalHandler( Node node, string methodName, SignalHandlerAttribute attr )
		{
			var signal = attr.SignalName;
			Node sender = null;

			if( !string.IsNullOrEmpty( attr.TargetNodeField ) )
			{
				MemberInfo[] members = TypeMembers[node.GetType()];
				MemberInfo? member = members.FirstOrDefault( mi => mi.Name == attr.TargetNodeField );

				sender = member?.GetValue( node ) as Node
					?? throw new Exception( $"SignalHandlerAttribute on '{node.GetType().FullName}.{methodName}', '{attr.TargetNodeField}' is a nonexistent field or property." );
			}
			else
			{
				sender = node;
			}

			if( sender == null )
			{
				throw new Exception( $"SignalHandlerAttribute on '{node.GetType().FullName}.{methodName}', '{attr.TargetNodeField}' is a null value, or property, unable to get." );
			}

			if( !sender.IsConnected( signal, new Callable( node, methodName ) ) )
			{
				sender.Connect( signal, new Callable( node, methodName ) );
			}
		}

		private static void ResolveNodeFromPath( Node node, MemberInfo member, string targetFieldName )
		{
			GD.Print( $"For {member.Name} " );

			var type = node.GetType();
			MemberInfo targetMember = type.GetField( targetFieldName ) is FieldInfo fi
						? new MemberInfo( fi )
						: type.GetProperty( targetFieldName ) is PropertyInfo pi
						? new MemberInfo( pi )
						: throw new Exception( $"ResolveNodeAttribute {targetFieldName} nonexistent field or property on {member.Name} of {type.FullName}" );

			NodePath path = targetMember.GetValue( node ) as NodePath
					?? throw new Exception( $"ResolveNodeAttribute on {type.FullName}.{member.Name} targets property {targetFieldName} which is not a NodePath" );

			AssignPathToMember( node, member, path );
		}

		private static void LoadResource( Node node, MemberInfo member, string resourcePath )
		{

			GD.Print( $"LoadResource {resourcePath} for type {node.GetType().Name} in {member.Name} of type {member.MemberType.Name}" );

			Resource res;
			try
			{
				res = GD.Load( resourcePath );
			}
			catch( Exception ex )
			{
				throw new Exception( $"Failed to load Resource '{resourcePath}', Message: '{ex.Message}'.", ex );
			}

			if( res == null )
			{
				throw new Exception( $"Failed to load Resource '{resourcePath}`, File not found!" );
			}

			try
			{
				member.SetValue( node, res );
			}
			catch( Exception ex )
			{
				throw new Exception( $"Failed to set variable {member.Name} with the {member.MemberType} for {resourcePath}.", ex );
			}
		}

		private static Node TryGetNode( Node node, List<string> names )
		{
			foreach( var name in names )
			{
				try
				{
					if( string.IsNullOrEmpty( name ) ) continue;
					var target = node.GetNodeOrNull( name );
					if( target != null ) return target;

					if( node.Owner == null )
					{
						target = node.FindChild( name, true, false );
						if( target != null ) return target;
						continue;
					}

					target = node.Owner.GetNodeOrNull( name );
					if( target != null ) return target;

					target = node.Owner.FindChild( name, true, false );
					if( target != null ) return target;

				}
				catch( Exception ex )
				{
					GD.PrintErr( $"TryGetNode {node.GetType().Name} broke on {name}" );
					GD.PrintErr( $"TryGetNode got ex {ex.Message}" );
				}
			}
			return null;
		}

		private static void LoadSingleton( Node node, MemberInfo member, string name )
		{
			var name1 = member.Name;
			if( !name1.StartsWith( "_" ) )
				name1 = string.Empty;
			else
			{
				name1 = char.ToUpperInvariant( member.Name[1] ) + member.Name[2..];
				// name1 = member.Name.Replace("_", string.Empty);
				// name1 = char.ToUpperInvariant(name1[0]) + name[1..];
			}
			List<string> names = new List<string>()
			{
				string.IsNullOrEmpty(name) ? name : $"/root/{name}",
				$"/root/{member.Name}",
				string.IsNullOrEmpty(name1) ? name1 : $"/root/{name1}",
				$"/root/{member.MemberType.Name}"
			};

			//You dont need to check for something, then handle it.  Just handle it
			//if (names.Contains(""))
			names.RemoveAll( string.IsNullOrEmpty );

			Node value = TryGetNode( node, names );

			if( value == null )
			{
				throw new Exception( $"Failed to load Singleton/Autoload for {member.MemberType.Name}.  Node was not found at /root with the following names: {string.Join( ",", names.ToArray() )}" );
			}
			try
			{
				member.SetValue( node, value );
			}
			catch( Exception ex )
			{
				throw new Exception( $"Failed to load Singleton/Autoload for {member.MemberType.Name}.  Error setting node value for {member.Name}.", ex );
			}
		}

		private static void AssignPathToMember( Node node, MemberInfo member, NodePath path )
		{
			var name1 = member.Name;
			if( !name1.StartsWith( "_" ) )
			{
				name1 = string.Empty;
			}
			else
			{
				name1 = char.ToUpperInvariant( member.Name[1] ) + member.Name[2..];
				// name1 = member.Name.Replace("_", string.Empty);
				// name1 = char.ToUpperInvariant(name1[0]) + name1.Substring(1);
			}

			var pathStr = path.ToString();
			var pathReplacedStr = pathStr.Replace( '_', '/' );

			var prefix = member.CustomAttributes?.FirstOrDefault( a => a?.GetType() == typeof( PrefixAttribute ) ) as PrefixAttribute;


			List<string> names = new List<string>()
			{
				pathStr,
				pathReplacedStr,
				member.Name,
				$"%{member.Name}",
				name1,
				string.IsNullOrEmpty(name1) ? "" : $"%{name1}",
				member.MemberType.Name
			};

			if( prefix != null )
			{
				names.Add( $"{prefix.Prefix}/{member.Name}" );
			}


			if( names.Contains( "" ) )
			{
				names.RemoveAll( string.IsNullOrEmpty );
			}

			Node value = TryGetNode( node, names );

			if( value == null )
			{
				var errStr = $"AssignPathToMember on {node.GetType().FullName}.{member.Name} - Unable to find node with the following names: {string.Join( ",", names.ToArray() )}";
				GD.PrintErr( errStr );
				throw new Exception( errStr );
			}

			try
			{
				/* */
				GD.Print( $"Setting {member.Name} from path {path}" );
				member.SetValue( node, value );
			}
			catch( ArgumentException ex )
			{
				GD.PrintErr( $"AssignPathToMember on {node.GetType().FullName}.{member.Name} - cannot set value of type {value?.GetType().Name} on field type {member.MemberType.Name}" );
				GD.PrintErr( $"Exception {ex.Message}" );
				//throw new Exception( $"AssignPathToMember on {node.GetType().FullName}.{member.Name} - cannot set value of type {value?.GetType().Name} on field type {member.MemberType.Name}", e );
			}
		}

		private readonly struct MemberInfo
		{
			public string Name { get; }
			public Type MemberType { get; }
			public IEnumerable<Attribute> CustomAttributes { get; }
			public Action<object, object> SetValue { get; }
			public Func<object, object> GetValue { get; }

			public MemberInfo( PropertyInfo pi )
			{
				this.Name = pi.Name;
				this.MemberType = pi.PropertyType;
				this.CustomAttributes = pi.GetCustomAttributes();
				this.SetValue = pi.SetValue;
				this.GetValue = pi.GetValue;
			}

			public MemberInfo( FieldInfo fi )
			{
				this.Name = fi.Name;
				this.MemberType = fi.FieldType;
				this.CustomAttributes = fi.GetCustomAttributes();
				this.SetValue = fi.SetValue;
				this.GetValue = fi.GetValue;
			}
		}

		private readonly struct SignalHandlerInfo
		{
			public string MethodName { get; }
			public SignalHandlerAttribute Attribute { get; }

			public SignalHandlerInfo( string methodName, SignalHandlerAttribute attr ) =>
				(MethodName, Attribute) = (methodName, attr);
		}

		private static readonly Dictionary<Type, MemberInfo[]> TypeMembers = new Dictionary<Type, MemberInfo[]>();
		private static readonly Dictionary<Type, SignalHandlerInfo[]> SignalHandlers = new Dictionary<Type, SignalHandlerInfo[]>();

	}
}