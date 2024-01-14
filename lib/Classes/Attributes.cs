using System;

namespace Godot.Sharp.Extras;


public class PrefixAttribute : Attribute
{

	public string Prefix { get; set; }

	public PrefixAttribute( string prefix )
	{
		Prefix = prefix;
	}

}