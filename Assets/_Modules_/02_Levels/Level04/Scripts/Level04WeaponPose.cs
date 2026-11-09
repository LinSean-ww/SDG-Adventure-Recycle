using SDGAdventure.CompletePlayer;
using SDGAdventure.SharedPlayer;
using UnityEngine;
namespace SDGAdventure.Level04 {
 [DefaultExecutionOrder(600)]
 public sealed class Level04WeaponPose : MonoBehaviour {
  private CompletePlayerWorkerAppearance worker;
  private PlayerPerspectiveRig rig;
  private Camera cam;
  private EcoQuantumWeaponView quantum;
  private bool owned;
  public void Bind(Camera camera){cam=camera;worker=camera?camera.GetComponentInParent<CompletePlayerWorkerAppearance>():null;rig=camera?camera.GetComponentInParent<PlayerPerspectiveRig>():null;quantum=GetComponent<EcoQuantumWeaponView>();}
  void LateUpdate(){
   if(!worker||!rig||!cam)return;
   if(!rig.IsThirdPerson){Release();if(!quantum)transform.localPosition=new Vector3(.22f,-.18f,.68f);return;}
   worker.SetWeaponPoseOwner(this);owned=true;
   Vector3 forward=Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.001f)forward=rig.transform.forward;
   Vector3 shoulders=(worker.LeftShoulder.position+worker.RightShoulder.position)*.5f;
   Vector3 right=Vector3.Cross(Vector3.up,forward);
   Vector3 position=shoulders+right*.18f+forward*.32f-Vector3.up*.08f;
   transform.SetPositionAndRotation(position,Quaternion.LookRotation(cam.transform.forward,Vector3.up));
   Transform main=quantum?quantum.PrimaryGrip:null, support=quantum?quantum.SupportGrip:null;
   Vector3 a=main?main.position:transform.TransformPoint(.03f,-.12f,-.12f),b=support?support.position:transform.TransformPoint(-.10f,-.08f,.20f);
   Solve(worker.RightShoulder,worker.RightElbow,worker.RightHand,a,1);Solve(worker.LeftShoulder,worker.LeftElbow,worker.LeftHand,b,-1);
  }
  void Solve(Transform shoulder,Transform elbow,Transform hand,Vector3 target,int side){if(!shoulder||!elbow||!hand)return;const float upper=.34f,lower=.33f;Vector3 delta=target-shoulder.position;float distance=Mathf.Clamp(delta.magnitude,.03f,upper+lower-.002f);Vector3 dir=delta.sqrMagnitude>.0001f?delta.normalized:rig.transform.forward;Vector3 pole=Vector3.ProjectOnPlane(rig.transform.right*side-Vector3.up*.35f-rig.transform.forward*.15f,dir);if(pole.sqrMagnitude<.0001f)pole=Vector3.ProjectOnPlane(Vector3.up,dir);pole.Normalize();float along=(upper*upper-lower*lower+distance*distance)/(2f*distance);float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));Vector3 bend=shoulder.position+dir*along+pole*height,end=shoulder.position+dir*distance;shoulder.rotation=Quaternion.FromToRotation(Vector3.down,bend-shoulder.position);elbow.rotation=Quaternion.FromToRotation(Vector3.down,end-bend);hand.rotation=transform.rotation;}
  void Release(){if(owned&&worker)worker.SetWeaponPoseOwner(null);owned=false;}
  void OnDisable()=>Release();
 }
}