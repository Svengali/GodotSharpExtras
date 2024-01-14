



using System;
using Godot;
using Godot.Sharp;
using Godot.NativeInterop;
using Godot.Bridge;
using Godot.Collections;




namespace Godot.Sharp.Extras;


public class CollisionInfo3
{
  public Vector3 position;
	public Vector3 normal;
	public GodotObject collider;
	//ObjectId collider_id;
	public ulong collider_id;
	public Rid rid;
	public int shape;
	public Variant metadata;

	public Node3D Node3D => collider as Node3D;
	public bool IsNode3D => Node3D != null;
}

static public class DictionaryExt
{
	public static CollisionInfo3 ToCollisionStruct( this Dictionary dict )
	{
		var info = new CollisionInfo3
		{
			position		= dict["position"].AsVector3(),
			normal			= dict["normal"].AsVector3(),
			collider		= dict["collider"].AsGodotObject(),
			//collider_id	= dict["collider_id"].AsUInt64(),
			//rid					= dict["rid"].AsRid(),
			//shape				= dict["shape"].AsInt32(),
			//metadata		= dict["metadata"],
		};

		return info;
	}



}

