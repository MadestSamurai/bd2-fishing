// Minimal collision/movement host for exercising the actual production navigation coordinator.
// These types are test-only; packaged components compile against the installed Unity assemblies.
using System.Collections;
using BD2Fishing.Runtime;
namespace UnityEngine {
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new();public static Vector3 up=>new(0,1,0);
  public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector3 normalized=>magnitude<1e-6?zero:this*(1/magnitude);
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
  public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;
  public override string ToString()=>$"({x:F2},{y:F2},{z:F2})";
 }
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public float magnitude=>(float)Math.Sqrt(x*x+y*y);}
 public static class Mathf {public const float PI=(float)Math.PI;public static float Cos(float x)=>(float)Math.Cos(x);public static float Sin(float x)=>(float)Math.Sin(x);public static float Clamp(float x,float lo,float hi)=>Math.Clamp(x,lo,hi);}
 public class GameObject {public bool activeInHierarchy=true;}
 public class Transform {public Vector3 position;}
 public class Component {static int next;readonly int id=++next;public GameObject gameObject=new();public Transform transform=new();public int GetInstanceID()=>id;}
 public class CharacterController:Component {public bool enabled=true;}
}
namespace UnityEngine.AI {
 using UnityEngine;
 public enum NavMeshPathStatus {PathComplete,PathInvalid}
 public class NavMeshPath {public Vector3[] corners=Array.Empty<Vector3>();public NavMeshPathStatus status=NavMeshPathStatus.PathComplete;}
 public struct NavMeshQueryFilter {public int agentTypeID,areaMask;}
 public struct NavMeshHit {public Vector3 position;}
 public static class NavMesh {public static bool SamplePosition(Vector3 p,out NavMeshHit hit,float distance,NavMeshQueryFilter f){hit=new(){position=p};return true;}}
 public class NavMeshAgent:Component {public bool isActiveAndEnabled=true,isOnNavMesh=true,hasPath;public int agentTypeID,areaMask=1;public float baseOffset;public float remainingDistance;public bool CalculatePath(Vector3 p,NavMeshPath path){path.corners=new[]{transform.position,p};return true;}}
}
namespace gamfs.Fishing {public class FishingCastingArea:UnityEngine.Component {public float diameter=1,height=1;}}
public class PlayerController:UnityEngine.Component {
 public PlayerMoveController Move=new();public int Starts,Rotations;
 public void SetRotationForce(UnityEngine.Vector3 direction){Rotations++;Move.Direction=direction;}
 public void SetMoveStart(){Starts++;Move.State="Moving";}
}
public class PlayerMoveController:UnityEngine.Component {
 public UnityEngine.AI.NavMeshAgent? Nav;public UnityEngine.CharacterController? Body=new();
 public string State="Stop",Mode="CharController";public int Stops,NavStarts;public UnityEngine.Vector3 Direction,Destination;
 public void ChangeMoveType(string mode){Mode=mode;}
 public void ClearMove(){}
 public void SetNavStop(){if(Nav!=null)Nav.hasPath=false;}
 public void StopMove(){Stops++;State="Stop";SetNavStop();}
 public bool SetMoveNav(UnityEngine.Vector3 point,object? callback,bool complete){NavStarts++;Destination=point;if(Nav==null||!Nav.isOnNavMesh)return false;Nav.hasPath=true;return true;}
}
namespace BD2Fishing.Runtime {
 using UnityEngine;
 internal class NavigationHost {public PlayerController Player=new();public Component Boat=new();public gamfs.Fishing.FishingCastingArea[] Areas=Array.Empty<gamfs.Fishing.FishingCastingArea>();}
 internal static class FishingBindings {
  public static NavigationHost Host=new();
  public static object Read(string role,object? owner)=>Host;
  public static object? Get(object? obj,string name)=>obj switch {
   NavigationHost h=>name switch {"ὬὪὧὦὭὬὪὧὣὣὫ"=>h.Player,"ὫὨὥὣὫὪὨὥὪὡὣ"=>h.Areas,"ὪὪὯὪὪὡὠὬὣὭὥ"=>h.Boat,_=>throw new Exception(name)},
   PlayerController p=>p.Move,
   PlayerMoveController m=>name switch {"ὦὣὠὨὣὨὡὯὪὢὦ"=>m.Nav,"ὫὬὤὪὠὧὨὩὬὬὩ"=>m.Body,"ὭὠὢὩὫὤὥὬὧὡὢ"=>m.State,"ὠὤὣὫὮὢὬὨὫὢὡ"=>m.Mode,_=>throw new Exception(name)},
   gamfs.Fishing.FishingCastingArea a=>name=="diameter"?a.diameter:a.height,
   _=>null};
  public static object? Call(object obj,string name,params object?[] args){var m=(PlayerMoveController)obj;if(name=="ChangeMoveType"){m.ChangeMoveType((string)args[0]!);return null;}return m.SetMoveNav((Vector3)args[0]!,args[1],(bool)args[2]!);}
  public static object EnumObject(string role,string name)=>name;
  public static double Num(object obj,string name)=>Convert.ToDouble(Get(obj,name));
  public static bool Active(Component? c)=>c!=null&&c.gameObject.activeInHierarchy;
 }
}
