using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SDGAdventure.SharedPlayer;
using Object=UnityEngine.Object;

namespace M6IntegrationTests
{
    public sealed class C3V12FinalFollowupTests
    {
        private C3V11ThirdPersonInteractionTests scope;
        private readonly List<GameObject> temporary=new List<GameObject>();
        private const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Type T(string n)=>C3V11Phase2RecyclingLineTests.GameType(n);
        private static object Get(object o,string n)=>C3V11Phase2RecyclingLineTests.Member(o,n);
        private static object Call(object o,string n,params object[] a)=>C3V11Phase2RecyclingLineTests.Call(o,n,a);
        private static void Set(object o,string n,object v)=>o.GetType().GetField(n,Any).SetValue(o,v);
        private static GameObject Player=>GameObject.Find("M2_Player");
        private static CharacterController Body=>Player.GetComponent<CharacterController>();
        private static Camera Camera=>Player.GetComponentInChildren<Camera>();
        private static Component Controller=>Player.GetComponent(T("C3TreasurePuzzle.C3FirstPersonController"));
        private static Component Rig=>Player.GetComponents<MonoBehaviour>().Single(c=>c && c.GetType().Name=="PlayerPerspectiveRig");
        private static Component Worker=>Player.GetComponent(T("C3TreasurePuzzle.C3WorkerAppearance"));
        private static Component Game=>T("EcoAdventureRuntime").GetProperty("Instance").GetValue(null) as Component;
        private static Component Service=>Type.GetType("SDGAdventure.SharedPlayer.PlayerInputService, SDGAdventure.SharedPlayer",true)
            .GetProperty("Instance").GetValue(null) as Component;
        private static Component Chip=>Object.FindObjectsOfType(T("C3TreasurePuzzle.C3CollectiblePart")).Cast<Component>()
            .Single(p=>(string)Get(p,"PartId")=="control_chip");
        private static void Mode(int m)=>typeof(C3V11ThirdPersonInteractionTests)
            .GetMethod("Mode",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{m});
        private static void Context(string n)
        {
            Call(Service,"SetContext",Enum.Parse(Get(Service,"Context").GetType(),n));
            PlayerCursor.LockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        private static Type InputType(string n)=>Type.GetType("UnityEngine.InputSystem."+n+", Unity.InputSystem",true);
        private void Keys(params string[] names)
        {
            Type key=InputType("Key");Array keys=Array.CreateInstance(key,names.Length);
            for(int i=0;i<names.Length;i++)keys.SetValue(Enum.Parse(key,names[i]),i);
            Type state=InputType("LowLevel.KeyboardState");object value=Activator.CreateInstance(state,new object[]{keys});
            MethodInfo queue=InputType("InputSystem").GetMethods(BindingFlags.Public|BindingFlags.Static)
                .Single(m=>m.Name=="QueueStateEvent" && m.IsGenericMethodDefinition && m.GetParameters().Length==3);
            queue.MakeGenericMethod(state).Invoke(null,new[]{Get(scope,"keyboard"),value,(object)(-1d)});
            InputType("InputSystem").GetMethod("Update",Type.EmptyTypes).Invoke(null,null);
        }
        private void Press(string key)
        {Keys();Keys(key);Call(Controller,"Update");Keys();}
        private void PressF()=>typeof(C3V11ThirdPersonInteractionTests).GetMethod("Press",Any).Invoke(scope,new object[]{false});
        private static void Tick(bool crouch=false,bool jump=false)
        {
            Call(Controller,"TickMovement",Vector2.zero,false,crouch,jump,.02f);
            Call(Worker,"LateUpdate");Call(Rig,"LateUpdate");Physics.SyncTransforms();
        }
        private static void Place(Vector3 p)
        {
            Body.enabled=false;Player.transform.SetPositionAndRotation(p,Quaternion.identity);
            Body.enabled=true;Physics.SyncTransforms();
        }
        private GameObject Box(string name,Vector3 centre,Vector3 size)
        {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;
            g.transform.position=centre;g.transform.localScale=size;temporary.Add(g);Physics.SyncTransforms();return g;
        }
        private void Park(int mode)
        {
            Mode(mode);Box("FOLLOWUP_TEST_FLOOR",new Vector3(128,127.9f,128),new Vector3(12,.2f,12));
            Set(Controller,"isProne",false);Set(Controller,"verticalSpeed",0f);
            Body.height=1.8f;Body.center=Vector3.up*.9f;
            Place(new Vector3(128,128.08f,128));
            for(int i=0;i<25;i++)Tick();
            Assert.IsTrue(Body.isGrounded);
        }
        private void Prone(int mode)
        {
            Park(mode);Press("Z");Assert.IsTrue((bool)Get(Controller,"isProne"));
            for(int i=0;i<25;i++)Tick();
            Assert.That(Body.height,Is.EqualTo(.75f).Within(.001f));Assert.IsTrue(Body.isGrounded);
        }
        [UnitySetUp] public IEnumerator Open()
        {
            scope=new C3V11ThirdPersonInteractionTests();yield return scope.Open();
            // The accepted fixture disables automatic controller updates. Drive
            // its real Update/TickMovement deterministically, with real keyboard
            // events and exactly one subscription to the existing input service.
            Assert.IsFalse(((Behaviour)Controller).enabled);
            Call(Controller,"UseSharedInput",Service);Call(Controller,"OnEnable");
        }
        [UnityTearDown] public IEnumerator Close()
        {
            if(Player)Call(Controller,"OnDisable");
            foreach(GameObject g in temporary)if(g)Object.Destroy(g);temporary.Clear();
            if(scope!=null)yield return scope.Close();scope=null;
        }

        [UnityTest] public IEnumerator FirstPersonZThenSpaceStandsBeforeOneGroundedJump(){yield return StandJump(0);}
        [UnityTest] public IEnumerator ThirdPersonZThenSpaceStandsBeforeOneGroundedJump(){yield return StandJump(1);}
        private IEnumerator StandJump(int mode)
        {
            Prone(mode);Press("Space");
            Assert.IsFalse((bool)Get(Controller,"isProne"));
            Assert.LessOrEqual((float)Get(Controller,"verticalSpeed"),0f,"No prone-height takeoff");
            int launches=0;bool wasRising=false;
            for(int i=0;i<80;i++)
            {
                Tick();float speed=(float)Get(Controller,"verticalSpeed");
                if(speed>0 && !wasRising){launches++;Assert.That(Body.height,Is.EqualTo(1.8f).Within(.001f));}
                wasRising=speed>0;
                Assert.That(Body.center.y,Is.EqualTo(Body.height*.5f).Within(.001f));
                if(mode==0)Assert.Less(Camera.transform.localPosition.y,Body.height-.1f);
            }
            Assert.AreEqual(1,launches);Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
            Assert.That((float)Get(Rig,"eyeHeight"),Is.EqualTo(1.5f).Within(.01f));
            if(mode==0)Assert.That(Camera.transform.localPosition.y,Is.EqualTo(1.5f).Within(.04f));
            Assert.That(((Transform)Get(Worker,"hips")).localPosition.y,Is.EqualTo(.88f).Within(.01f));
            yield return null;
        }
        [UnityTest] public IEnumerator LowCeilingRejectsSpaceInBothViewsAndZStillRecoversAfterLeaving()
        {
            Prone(0);GameObject roof=Box("FOLLOWUP_LOW_ROOF",new Vector3(128,129.0f,128),new Vector3(3,.18f,3));
            foreach(int mode in new[]{0,1})
            {
                Mode(mode);Press("Space");for(int i=0;i<10;i++)Tick();
                Assert.IsTrue((bool)Get(Controller,"isProne"));
                Assert.That(Body.height,Is.EqualTo(.75f).Within(.001f));
                Assert.LessOrEqual((float)Get(Controller,"verticalSpeed"),0f);Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
                Assert.Less(Body.bounds.max.y,roof.GetComponent<Collider>().bounds.min.y);
            }
            roof.SetActive(false);Physics.SyncTransforms();Press("Z");
            for(int i=0;i<30;i++)Tick();
            Assert.IsFalse((bool)Get(Controller,"isProne"));Assert.That(Body.height,Is.EqualTo(1.8f).Within(.001f));
            yield return null;
        }
        [UnityTest] public IEnumerator NewCeilingDuringRecoveryCancelsJumpAndReturnsToProne()
        {
            Prone(0);Press("Space");float oldHeight=Body.height;
            Box("FOLLOWUP_MOVING_ROOF",new Vector3(128,129.08f,128),new Vector3(3,.1f,3));
            Tick();Assert.IsTrue((bool)Get(Controller,"isProne"));Assert.LessOrEqual(Body.height,oldHeight);
            Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));yield return null;
        }
        [UnityTest] public IEnumerator AirborneSpaceCannotQueueALandingOrDoubleJump()
        {
            Prone(1);Place(Player.transform.position+Vector3.up*3f);Body.Move(Vector3.down*.001f);
            Assert.IsFalse(Body.isGrounded);Set(Controller,"verticalSpeed",-1f);Press("Space");
            Assert.IsFalse((bool)Get(Controller,"isProne"));Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
            for(int i=0;i<120;i++){Tick();Assert.LessOrEqual((float)Get(Controller,"verticalSpeed"),0f);}
            Assert.IsTrue(Body.isGrounded);yield return null;
        }
        [UnityTest] public IEnumerator UiSpaceDoesNotUnproneOrJumpInEitherView()
        {
            Prone(0);
            foreach(int mode in new[]{0,1})
            {
                Mode(mode);Context("UI");float height=Body.height;Press("Space");
                Assert.IsTrue((bool)Get(Controller,"isProne"));Assert.AreEqual(height,Body.height);
                Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
            }
            Context("Gameplay");yield return null;
        }
        [UnityTest] public IEnumerator UiAndUnlockedCursorCancelAnExistingBufferedJump()
        {
            Prone(0);Press("Space");Assert.Greater((float)Get(Controller,"proneJumpRemaining"),0f);
            Context("UI");Call(Controller,"Update");Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
            Context("Gameplay");for(int i=0;i<40;i++){Tick();Assert.LessOrEqual((float)Get(Controller,"verticalSpeed"),0f);}
            Press("Z");for(int i=0;i<25;i++)Tick();Press("Space");
            Assert.Greater((float)Get(Controller,"proneJumpRemaining"),0f);
            PlayerCursor.LockState=CursorLockMode.None;Call(Controller,"Update");
            Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));PlayerCursor.LockState=CursorLockMode.Locked;
            yield return null;
        }
        [UnityTest] public IEnumerator HeldCtrlUnpronesToCrouchWithoutADelayedJump()
        {
            Prone(0);Tick(true,true);
            Assert.IsFalse((bool)Get(Controller,"isProne"));Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
            for(int i=0;i<25;i++)Tick(true);
            Assert.That(Body.height,Is.EqualTo(1.1f).Within(.001f));
            for(int i=0;i<30;i++){Tick();Assert.LessOrEqual((float)Get(Controller,"verticalSpeed"),0f);}
            Assert.That(Body.height,Is.EqualTo(1.8f).Within(.001f));
            Press("Z");for(int i=0;i<25;i++)Tick();Assert.IsTrue((bool)Get(Controller,"isProne"));yield return null;
        }
        [UnityTest] public IEnumerator ZCancelsAPendingProneJump()
        {
            Prone(0);Press("Space");Press("Z");
            Assert.IsTrue((bool)Get(Controller,"isProne"));Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
            for(int i=0;i<40;i++){Tick();Assert.LessOrEqual((float)Get(Controller,"verticalSpeed"),0f);}
            yield return null;
        }
        [UnityTest] public IEnumerator SlowStandExpiresTheRequestInsteadOfJumpingMuchLater()
        {
            Prone(0);Set(Controller,"stanceSpeed",.1f);Press("Space");
            for(int i=0;i<40;i++)Tick();Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
            Assert.Less(Body.height,1.8f);Set(Controller,"stanceSpeed",3f);
            for(int i=0;i<30;i++){Tick();Assert.LessOrEqual((float)Get(Controller,"verticalSpeed"),0f);}
            yield return null;
        }
        [UnityTest] public IEnumerator JumpDisabledStillAllowsSafeStandingButNoImpulse()
        {
            Prone(1);Set(Controller,"allowJump",false);Press("Space");
            Assert.IsFalse((bool)Get(Controller,"isProne"));
            for(int i=0;i<35;i++){Tick();Assert.LessOrEqual((float)Get(Controller,"verticalSpeed"),0f);}
            Assert.That(Body.height,Is.EqualTo(1.8f).Within(.001f));Assert.AreEqual(0f,Get(Controller,"proneJumpRemaining"));
            yield return null;
        }
        [UnityTest] public IEnumerator ViewSwitchDuringRecoveryKeepsExactlyOneJumpSubscriptionAndTakeoff()
        {
            Prone(0);Press("Space");
            for(int i=0;i<6;i++)
            {
                Mode(i%2);FieldInfo field=Service.GetType().GetField("JumpPressed",Any);Assert.IsNotNull(field);
                Delegate listeners=field.GetValue(Service) as Delegate;
                Assert.IsNotNull(listeners);Assert.AreEqual(1,listeners.GetInvocationList().Count(d=>d.Target==Controller));
                Tick();
            }
            int takeoffs=0;bool rising=false;
            for(int i=0;i<75;i++)
            {
                Tick();bool positive=(float)Get(Controller,"verticalSpeed")>0;
                if(positive && !rising)takeoffs++;rising=positive;
            }
            Assert.AreEqual(1,takeoffs);yield return null;
        }

        [UnityTest] public IEnumerator MaintenanceBoardRestsOnTableAndStaysSeparateFromTheUnmovedQuestChip()
        {
            Transform art=GameObject.Find("UpperChipMaintenanceBench").transform.Find("V11_StationArt_UpperChipMaintenanceBench");
            Transform board=art.Find("MaintenanceCircuitBoard");Assert.IsNotNull(board);
            Renderer pcb=board.GetComponentsInChildren<Renderer>().Single(r=>r.name=="FibreglassPCB");
            Renderer table=art.Find("WeatheredSteelWorktop").GetComponent<Renderer>();
            Assert.That(pcb.bounds.min.y-table.bounds.max.y,Is.InRange(-.001f,.002f));
            Assert.Greater(Quaternion.Angle(board.rotation,Chip.transform.rotation),20f);
            foreach(Renderer r in board.GetComponentsInChildren<Renderer>())
            {
                Assert.Greater(r.bounds.min.x,table.bounds.min.x);Assert.Less(r.bounds.max.x,table.bounds.max.x);
                Assert.Greater(r.bounds.min.z,table.bounds.min.z);Assert.Less(r.bounds.max.z,table.bounds.max.z);
                Assert.Less(r.bounds.max.x,Chip.GetComponent<Collider>().bounds.min.x-.1f);
            }
            Assert.Less(Vector3.Distance(Chip.transform.position,new Vector3(-13.2f,5.37f,20.52f)),.001f);
            Renderer pad=art.Find("ChipESDSupport").GetComponent<Renderer>();
            Bounds chipVisual=Chip.transform.Find("FactoryPartVisual").GetComponentsInChildren<Renderer>()
                .Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            Assert.That(pad.bounds.min.y-table.bounds.max.y,Is.InRange(-.001f,.002f));
            Assert.That(chipVisual.min.y-pad.bounds.max.y,Is.InRange(-.001f,.002f));
            Assert.Greater(chipVisual.min.x,pad.bounds.min.x);Assert.Less(chipVisual.max.x,pad.bounds.max.x);
            Assert.Greater(chipVisual.min.z,pad.bounds.min.z);Assert.Less(chipVisual.max.z,pad.bounds.max.z);
            Assert.IsFalse(board.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled));yield return null;
        }
        [UnityTest] public IEnumerator PliersHaveOnePivotClearTipsAndARealPegWithoutTouchingOtherTools()
        {
            Transform art=GameObject.Find("UpperChipMaintenanceBench").transform.Find("V11_StationArt_UpperChipMaintenanceBench");
            Transform pliers=art.Find("MaintenancePliers");Assert.IsNotNull(pliers);
            Assert.AreEqual(1,pliers.GetComponentsInChildren<Transform>().Count(t=>t.name=="PliersPivot"));
            Renderer pegboard=art.Find("ToolPegboard").GetComponent<Renderer>();
            foreach(string name in new[]{"PliersLeft","PliersRight"})
            {
                Transform handle=pliers.Find(name);Renderer r=handle.GetComponent<Renderer>();
                Assert.Less(r.bounds.max.z,pegboard.bounds.min.z);
                Assert.Greater(r.bounds.min.x,art.position.x+.42f,"Handle crosses the X=.38 screwdriver bay");
                Vector3[] tips=handle.GetComponent<MeshFilter>().sharedMesh.vertices.Where(v=>v.y>.5f)
                    .Select(v=>pliers.InverseTransformPoint(handle.TransformPoint(v))).ToArray();
                Assert.IsNotEmpty(tips);
                Assert.IsTrue(tips.All(v=>name=="PliersLeft"?v.x<-.02f:v.x>.02f));
            }
            Renderer peg=art.Find("PliersPeg").GetComponent<Renderer>();
            Assert.GreaterOrEqual(peg.bounds.max.z,pegboard.bounds.min.z-.005f);
            Assert.IsFalse(pliers.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled));yield return null;
        }
        [UnityTest] public IEnumerator MaintenanceChipStillAcceptsRealFOnceInFirstPerson(){yield return CollectChip(0);}
        [UnityTest] public IEnumerator MaintenanceChipStillAcceptsRealFOnceInThirdPerson(){yield return CollectChip(1);}
        private IEnumerator CollectChip(int mode)
        {
            Mode(mode);Place(new Vector3(-13.2f,4.58f,19.3f));
            Vector3 eye=new Vector3(-13.2f,6.0f,mode==0?19.3f:18.7f);
            Component part=Chip;Camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(part.GetComponent<Collider>().bounds.center-eye));
            Component interactor=Player.GetComponent(T("C3TreasurePuzzle.C3PlayerInteractor"));
            Call(interactor,"SetCamera",Camera);Physics.SyncTransforms();
            Assert.AreSame(part,Call(interactor,"FindTarget"));PressF();Assert.IsFalse(part.gameObject.activeSelf);
            PressF();Assert.AreEqual(1,((IEnumerable)Get(Game,"inventory")).Cast<object>()
                .Where(e=>(string)Get(e,"id")=="control_chip").Sum(e=>(int)Get(e,"quantity")));
            Assert.AreEqual(1,Get(C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3TreasureProgress"),"CollectedCount"));
            yield return null;
        }
        [UnityTest] public IEnumerator NormalFactoryHasNoDemoInteractionOrTerminalAcrossThreeLoads()
        {
            for(int i=0;i<3;i++)
            {
                Assert.IsNull(GameObject.Find("M2_TestInteractable"));
                Assert.IsEmpty(Object.FindObjectsOfType(T("C3TreasurePuzzle.C3TestInteractable"),true));
                Assert.IsFalse(Object.FindObjectsOfType<Transform>(true).Any(t=>t.name=="FactoryTestTerminalVisual"));
                Assert.AreEqual(3,Object.FindObjectsOfType(T("C3TreasurePuzzle.C3CollectiblePart")).Length);
                Assert.IsNotNull(GameObject.Find("M4_PuzzleConsole"));
                if(i<2){yield return Close();yield return Open();}
            }
        }
        [UnityTest] public IEnumerator DedicatedTestsCanStillInstantiateTheDemoWithoutQuestRewards()
        {
            GameObject g=Box("FOLLOWUP_TEST_ONLY_INTERACTABLE",new Vector3(128,130,128),Vector3.one);
            Component demo=g.AddComponent(T("C3TreasurePuzzle.C3TestInteractable"));
            Assert.AreEqual("測試互動物件",Get(demo,"DisplayName"));Call(demo,"Interact",Player);
            Assert.AreEqual(0,Get(C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3TreasureProgress"),"CollectedCount"));
            yield return null;
        }
    }
}
