﻿//#define DEBUG
using UnityEngine;
using System.Collections;
using Pathfinding;

/** AI for following paths.
 * This AI is the default movement script which comes with the A* Pathfinding Project.
 * It is in no way required by the rest of the system, so feel free to write your own. But I hope this script will make it easier
 * to set up movement for the characters in your game. This script is not written for high performance, so I do not recommend using it for large groups of units.
 * \n
 * \n
 * This script will try to follow a target transform, in regular intervals, the path to that target will be recalculated.
 * It will on FixedUpdate try to move towards the next point in the path.
 * However it will only move in the forward direction, but it will rotate around it's Y-axis
 * to make it reach the target.
 * 
 * \section variables Quick overview of the variables
 * In the inspector in Unity, you will see a bunch of variables. You can view detailed information further down, but here's a quick overview.\n
 * The #repathRate determines how often it will search for new paths, if you have fast moving targets, you might want to set it to a lower value.\n
 * The #target variable is where the AI will try to move, it can be a point on the ground where the player has clicked in an RTS for example.
 * Or it can be the player object in a zombie game.\n
 * The speed is self-explanatory, so is turningSpeed, however #slowdownDistance might require some explanation.
 * It is the approximate distance from the target where the AI will start to slow down. Note that this doesn't only affect the end point of the path
 * but also any intermediate points, so be sure to set #forwardLook and #pickNextWaypointDist to a higher value than this.\n
 * #pickNextWaypointDist is simply determines within what range it will switch to target the next waypoint in the path.\n
 * #forwardLook will try to calculate an interpolated target point on the current segment in the path so that it has a distance of #forwardLook from the AI\n
 * Below is an image illustrating several variables as well as some internal ones, but which are relevant for understanding how it works.
 * Note that the #forwardLook range will not match up exactly with the target point practically, even though that's the goal.
 * \shadowimage{aipath_variables.png}
 * This script has many movement fallbacks.
 * If it finds a NavmeshController, it will use that, otherwise it will look for a character controller, then for a rigidbody and if it hasn't been able to find any
 * it will use Transform.Translate which is guaranteed to always work.
 */
[RequireComponent(typeof(Seeker))]
public class AIPath2 : MonoBehaviour {
	
	/** Determines how often it will search for new paths. 
	 * If you have fast moving targets or AIs, you might want to set it to a lower value.
	 * The value is in seconds between path requests.
	 */
	public float repathRate = 0.5F;
	
	/** Target to move towards.
	 * The AI will try to follow/move towards this target.
	 * It can be a point on the ground where the player has clicked in an RTS for example, or it can be the player object in a zombie game.
	 */
	public Transform target;
	public Transform spartan1;
	public Transform spartan2;
	public Transform spartan3;
	
	/** Enables or disables searching for paths.
	 * Setting this to false does not stop any active path requests from being calculated or stop it from continuing to follow the current path.
	 * \see #canMove
	 */
	public bool canSearch = true;
	
	/** Enables or disables movement.
	  * \see #canSearch */
	public bool canMove = true;
	
	/** Maximum velocity.
	 * This is the maximum speed in world units per second.
	 */
	public float speed = 3;
	
	/** Rotation speed.
	 * Rotation is calculated using Quaternion.SLerp. This variable represents the damping, the higher, the faster it will be able to rotate.
	 */
	public float turningSpeed = 5;
	
	/** Distance from the target point where the AI will start to slow down.
	 * Note that this doesn't only affect the end point of the path
 	 * but also any intermediate points, so be sure to set #forwardLook and #pickNextWaypointDist to a higher value than this
 	 */
	public float slowdownDistance = 0.6F;
	
	/** Determines within what range it will switch to target the next waypoint in the path */
	public float pickNextWaypointDist = 2;
	
	/** Target point is Interpolated on the current segment in the path so that it has a distance of #forwardLook from the AI.
	  * See the detailed description of AIPath for an illustrative image */
	public float forwardLook = 1;
	
	/** Distance to the end point to consider the end of path to be reached.
	 * When this has been reached, the AI will not move anymore until the target changes and OnTargetReached will be called.
	 */
	public float endReachedDistance = 0.2F;
	
	/** Do a closest point on path check when receiving path callback.
	 * Usually the AI has moved a bit between requesting the path, and getting it back, and there is usually a small gap between the AI
	 * and the closest node.
	 * If this option is enabled, it will simulate, when the path callback is received, movement between the closest node and the current
	 * AI position. This helps to reduce the moments when the AI just get a new path back, and thinks it ought to move backwards to the start of the new path
	 * even though it really should just proceed forward.
	 */
	public bool closestOnPathCheck = true;
	
	protected float minMoveScale = 0.05F;
	
	/** Cached Seeker component */
	protected Seeker seeker;
	
	/** Cached Transform component */
	protected Transform tr;
	
	/** Time when the last path request was sent */
	private float lastRepath = -9999;
	
	/** Current path which is followed */
	protected Path path;
	
	/** Cached CharacterController component */
	protected CharacterController controller;
	
	/** Cached NavmeshController component */
	protected NavmeshController navController;
	
	/** Cached Rigidbody component */
	protected Rigidbody rigid;
	
	/** Current index in the path which is current target */
	protected int currentWaypointIndex = 0;
	
	/** Holds if the end-of-path is reached
	 * \see TargetReached */
	protected bool targetReached = false;
	
	/** Only when the previous path has been returned should be search for a new path */
	protected bool canSearchAgain = true;
	
	/** Returns if the end-of-path has been reached
	 * \see targetReached */
	public bool TargetReached {
		get {
			return targetReached;
		}
	}
	
	// Use this for initialization
	void Awake () {
		seeker = GetComponent<Seeker>();
		//This is a simple optimization, cache the transform component lookup
		tr = transform;
		seeker.pathCallback += OnPathComplete;
		
		controller = GetComponent<CharacterController>();
		navController = GetComponent<NavmeshController>();
		rigid = rigidbody;
	}
	
	
	public void Start () {
		StartCoroutine (RepeatTrySearchPath ());
		
	}
	
	public IEnumerator RepeatTrySearchPath () {
//		while (true) 
//		{
//			float distance = 100;
//			foreach (var human in Spawn.humans) {
//				//if (Danger_Zone
//			}
//
//
//
//
//		}


//		while (true) {
//			if(Danger_Zone.distance3<=Danger_Zone.distance2)
//			{
//				target=spartan3;
//			}
//			else 
//			{
//				target=spartan2;
//			}
//			
			TrySearchPath ();
			yield return new WaitForSeconds (repathRate);
//		}
	}
	
	public void TrySearchPath () {
		
		if (Time.time - lastRepath >= repathRate && canSearchAgain) {
			SearchPath ();
		} else {
			StartCoroutine (WaitForRepath ());
		}
	}
	
	private bool waitingForRepath = false;
	protected IEnumerator WaitForRepath () {
		if (waitingForRepath) yield break; //A coroutine is already running
		
		waitingForRepath = true;
		//Wait until it is predicted that the AI should search for a path again
		yield return new WaitForSeconds (repathRate - (Time.time-lastRepath));
		
		waitingForRepath = false;
		//Try to search for a path again
		TrySearchPath ();
	}
	
	public void SearchPath () {
		//DELETE AGAIN!!!
		if (target == null) { Debug.LogError ("test"); return; }

		if (target == null) { Debug.LogError ("Target is null, aborting all search"); return; }
		
		lastRepath = Time.time;
		//This is where we should search to
		Vector3 targetPoint = target.position;
		
		canSearchAgain = false;
		
		//We should search from the current position
		Path p = new Path(GetFeetPosition(),targetPoint,null);
		seeker.StartPath (p);
		//seeker.StartPath (tr.position, targetPoint);
	}
	
	public virtual void OnTargetReached () {
		//End of path has been reached
		//If you want custom logic for when the AI has reached it's destination
		//add it here
		//You can also create a new script which inherits from this one
		//and override the function in that script
	}
	
	/** Called when a requested path has finished calculation.
	  * A path is first requested by #SearchPath(), it is then calculated, probably in the same or the next frame.
	  * Finally it is returned to the seeker which forwards it to this function.\n
	  */
	public void OnPathComplete (Path p) {
		path = p;
		currentWaypointIndex = 0;
		targetReached = false;
		canSearchAgain = true;
		
		//The next row can be used to find out if the path could be found or not
		//If it couldn't (error == true), then a message has probably been logged to the console
		//however it can also be got using p.errorLog
		//if (p.error)
		
		if (closestOnPathCheck) {
			Vector3 p1 = p.startPoint;
			Vector3 p2 = GetFeetPosition ();
			float magn = Vector3.Distance (p1,p2);
			Vector3 dir = p2-p1;
			dir /= magn;
			int steps = (int)(magn/pickNextWaypointDist);
			for (int i=0;i<steps;i++) {
				CalculateVelocity (p1);
				p1 += dir;
			}
			#if DEBUG
			Debug.DrawLine (p1,p2,Color.red,1);
			#endif
		}
		
		TrySearchPath ();
	}
	
	public virtual Vector3 GetFeetPosition () {
		if (controller != null)
			return tr.position - Vector3.up*controller.height*0.5F;
		
		return tr.position;
	}
	
	public void FixedUpdate () {
		
		if (!canMove) { return; }
		
		Vector3 dir = CalculateVelocity (GetFeetPosition());
		
		//Rotate towards targetDirection (filled in by CalculateVelocity)
		if (targetDirection != Vector3.zero) {
			RotateTowards (targetDirection);
		}
		
		if (navController != null) {
			navController.SimpleMove (GetFeetPosition(),dir);
		} else if (controller != null) {
			controller.SimpleMove (dir);
		} else if (rigid != null) {
			rigid.AddForce (dir);
		} else {
			transform.Translate (dir);
		}
	}
	
	/** Point to where the AI is heading.
	  * Filled in by #CalculateVelocity */
	protected Vector3 targetPoint;
	/** Relative direction to where the AI is heading.
	 * Filled in by #CalculateVelocity */
	protected Vector3 targetDirection;
	
	protected float XZSqrMagnitude (Vector3 a, Vector3 b) {
		float dx = b.x-a.x;
		float dz = b.z-a.z;
		return dx*dx + dz*dz;
	}
	
	/** Calculates desired velocity.
	 * Finds the target path segment and returns the forward direction, scaled with speed.
	 * A whole bunch of restrictions on the velocity is applied to make sure it doesn't overshoot, does not look too far ahead,
	 * and slows down when close to the target.
	 * /see speed
	 * /see endReachedDistance
	 * /see slowdownDistance
	 * /see CalculateTargetPoint
	 * /see targetPoint
	 * /see targetDirection
	 * /see currentWaypointIndex
	 */
	protected Vector3 CalculateVelocity (Vector3 currentPosition) {
		if (path == null || path.vectorPath == null || path.vectorPath.Length == 0) return Vector3.zero; 
		
		Vector3[] vPath = path.vectorPath;
		//Vector3 currentPosition = GetFeetPosition();
		
		if (vPath.Length == 1) {
			vPath = new Vector3[2] {currentPosition,vPath[0]};
		}
		
		if (currentWaypointIndex >= vPath.Length) { currentWaypointIndex = vPath.Length-1; }
		
		if (currentWaypointIndex <= 1) currentWaypointIndex = 1;
		
		while (true) {
			if (currentWaypointIndex < vPath.Length-1) {
				//There is a "next path segment"
				float dist = XZSqrMagnitude (vPath[currentWaypointIndex], currentPosition);
				//Mathfx.DistancePointSegmentStrict (vPath[currentWaypointIndex+1],vPath[currentWaypointIndex+2],currentPosition);
				if (dist < pickNextWaypointDist*pickNextWaypointDist) {
					currentWaypointIndex++;
				} else {
					break;
				}
			} else {
				break;
			}
		}
		
		Vector3 dir = vPath[currentWaypointIndex] - vPath[currentWaypointIndex-1];
		Vector3 targetPoint = CalculateTargetPoint (currentPosition,vPath[currentWaypointIndex-1] , vPath[currentWaypointIndex]);
		//vPath[currentWaypointIndex] + Vector3.ClampMagnitude (dir,forwardLook);
		
		
		
		dir = targetPoint-currentPosition;
		dir.y = 0;
		float targetDist = dir.magnitude;
		
		float slowdown = Mathf.Clamp01 (targetDist / slowdownDistance);
		
		this.targetDirection = dir;
		this.targetPoint = targetPoint;
		
		if (currentWaypointIndex == vPath.Length-1 && targetDist <= endReachedDistance) {
			if (!targetReached) { targetReached = true; OnTargetReached (); }
			
			//Send a move request, this ensures gravity is applied
			return Vector3.zero;
		}
		
		Vector3 forward = tr.forward;
		float dot = Vector3.Dot (dir.normalized,forward);
		float sp = speed * Mathf.Max (dot,minMoveScale) * slowdown;
		
		#if DEBUG
		Debug.DrawLine (vPath[currentWaypointIndex-1] , vPath[currentWaypointIndex],Color.black);
		Debug.DrawLine (GetFeetPosition(),targetPoint,Color.red);
		Debug.DrawRay (targetPoint,Vector3.up, Color.red);
		Debug.DrawRay (GetFeetPosition(),dir,Color.yellow);
		Debug.DrawRay (GetFeetPosition(),forward*sp,Color.cyan);
		#endif
		
		if (Time.fixedDeltaTime	> 0) {
			sp = Mathf.Clamp (sp,0,targetDist/(Time.fixedDeltaTime*2));
		}
		return forward*sp;
	}
	
	/** Rotates in the specified direction.
	 * Rotates around the Y-axis.
	 * \see turningSpeed
	 */
	protected virtual void RotateTowards (Vector3 dir) {
		Quaternion rot = tr.rotation;
		Quaternion target = Quaternion.LookRotation (dir);
		
		rot = Quaternion.Slerp (rot,target,turningSpeed*Time.fixedDeltaTime);
		Vector3 euler = rot.eulerAngles;
		euler.z = 0;
		euler.x = 0;
		rot = Quaternion.Euler (euler);
		
		tr.rotation = rot;
	}
	
	/** Calculates target point from the current line segment.
	 * \param p Current position
	 * \param a Line segment start
	 * \param b Line segment end
	 * The returned point will lie somewhere on the line segment.
	 * \see #forwardLook
	 * \todo This function uses .magnitude quite a lot, can it be optimized?
	 */
	protected Vector3 CalculateTargetPoint (Vector3 p, Vector3 a, Vector3 b) {
		a.y = p.y;
		b.y = p.y;
		
		float magn = (a-b).magnitude;
		if (magn == 0) return a;
		
		float closest = Mathfx.Clamp01 (Mathfx.NearestPointFactor (a, b, p));
		Vector3 point = (b-a)*closest + a;
		float distance = (point-p).magnitude;
		
		float lookAhead = Mathf.Clamp (forwardLook - distance, 0.0F, forwardLook);
		
		float offset = lookAhead / magn;
		offset = Mathf.Clamp (offset+closest,0.0F,1.0F);
		return (b-a)*offset + a;
	}
}
