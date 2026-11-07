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
    // A temporary Keyboard sends real InputSystem state events through the
    // EXISTING action service. Reflection keeps the accepted test asmdef intact.
    // No second InputService, substitute collectible, or direct Interact success.
    public sealed class C3V11ThirdPersonInteractionTests
    {
        private C3V11Phase2RecyclingLineTests scope;
        private object keyboard,previousKeyboard;
        private object inputSettings,previousBackground,previousEditorInput;
        private readonly List<GameObject> temporary=new List<GameObject>();
        private const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        private static readonly Vector3 Parking=new Vector3(128,128,128);
        private static Type TypeOf(string name) => C3V11Phase2RecyclingLineTests.GameType(name);
        private static Type InputType(string name) => Type.GetType("UnityEngine.InputSystem."+name+", Unity.InputSystem",true);
        private static object Get(object owner,string name) => C3V11Phase2RecyclingLineTests.Member(owner,name);
        private static object Call(object owner,string name,params object[] args) => C3V11Phase2RecyclingLineTests.Call(owner,name,args);
        private static GameObject Player => GameObject.Find("M2_Player");
        private static Component Game => TypeOf("EcoAdventureRuntime").GetProperty("Instance").GetValue(null) as Component;
        private static Component Interactor => Player.GetComponent(TypeOf("C3TreasurePuzzle.C3PlayerInteractor"));
        private static Camera Camera => Player.GetComponentInChildren<Camera>();
        private static Component Rig => Player.GetComponents<MonoBehaviour>().Single(c=>c && c.GetType().Name=="PlayerPerspectiveRig");
        private static Component Service => Type.GetType("SDGAdventure.SharedPlayer.PlayerInputService, SDGAdventure.SharedPlayer",true)
            .GetProperty("Instance").GetValue(null) as Component;
        private static int Quantity(string id) => ((IEnumerable)Get(Game,"inventory")).Cast<object>()
            .Where(item=>(string)Get(item,"id")==id).Sum(item=>(int)Get(item,"quantity"));
        private static Component Part(string id) => Object.FindObjectsOfType(TypeOf("C3TreasurePuzzle.C3CollectiblePart"))
            .Cast<Component>().Single(p=>(string)Get(p,"PartId")==id);
        private static void Context(string name)
        {
            Call(Service,"SetContext",Enum.Parse(Get(Service,"Context").GetType(),name));
            PlayerCursor.LockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        private static void Mode(int value)
        {
            PropertyInfo mode=Rig.GetType().Assembly.GetType("SDGAdventure.SharedPlayer.PlayerViewPreference",true).GetProperty("Mode");
            mode.SetValue(null,Enum.ToObject(mode.PropertyType,value));Call(Rig,"ApplyMode");
        }
        private static void Aim(Collider target,bool third,float bodyDistance=1.3f,bool highCamera=false)
        {
            Mode(third?1:0);
            Player.GetComponent<CharacterController>().enabled=false;
            Player.transform.SetPositionAndRotation(Parking,Quaternion.identity);
            target.transform.position=Parking+new Vector3(0,.9f,bodyDistance);
            Physics.SyncTransforms();
            Vector3 origin=third?Parking+new Vector3(.42f,highCamera?3.1f:1.4f,-3f):Parking+Vector3.up*1.4f;
            Camera.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(target.bounds.center-origin));
            Call(Interactor,"SetCamera",Camera);Call(Game,"BindCamera");Physics.SyncTransforms();
        }
        private void KeyboardState(bool down)
        {
            Type state=InputType("LowLevel.KeyboardState"),key=InputType("Key");
            Array keys=Array.CreateInstance(key,down?1:0);if(down)keys.SetValue(Enum.Parse(key,"F"),0);
            object value=Activator.CreateInstance(state,new object[]{keys});
            MethodInfo queue=InputType("InputSystem").GetMethods(BindingFlags.Public|BindingFlags.Static)
                .Single(m=>m.Name=="QueueStateEvent" && m.IsGenericMethodDefinition && m.GetParameters().Length==3);
            queue.MakeGenericMethod(state).Invoke(null,new[]{keyboard,value,(object)(-1d)});
            InputType("InputSystem").GetMethod("Update",Type.EmptyTypes).Invoke(null,null);
        }
        private void Press(bool runtimeFirst=false)
        {
            KeyboardState(false);KeyboardState(true);
            // Exercise both actual Update paths in either order for the SAME F
            // state event. Exactly one handler may dispatch a gameplay action.
            if(runtimeFirst)Call(Game,"Update");
            Call(Interactor,"Update");
            if(!runtimeFirst)Call(Game,"Update");
            KeyboardState(false);
        }
        [UnitySetUp] public IEnumerator Open()
        {
            keyboard=previousKeyboard=null;
            temporary.Clear();scope=new C3V11Phase2RecyclingLineTests();yield return scope.Open();
            PlayerCursor.BeginSimulation(CursorLockMode.None);
            inputSettings=InputType("InputSystem").GetProperty("settings").GetValue(null);
            PropertyInfo background=inputSettings.GetType().GetProperty("backgroundBehavior");
            PropertyInfo editorInput=inputSettings.GetType().GetProperty("editorInputBehaviorInPlayMode");
            previousBackground=background.GetValue(inputSettings);previousEditorInput=editorInput.GetValue(inputSettings);
            background.SetValue(inputSettings,Enum.Parse(background.PropertyType,"IgnoreFocus"));
            editorInput.SetValue(inputSettings,Enum.Parse(editorInput.PropertyType,"AllDeviceInputAlwaysGoesToGameView"));
            ((Behaviour)Rig).enabled=false; // fixture owns the camera pose in these selection tests
            Assert.IsNotNull(Service);Call(Interactor,"UseSharedInput",Service);((Behaviour)Interactor).enabled=true;Context("Gameplay");
            previousKeyboard=InputType("Keyboard").GetProperty("current").GetValue(null);
            keyboard=InputType("InputSystem").GetMethod("AddDevice",new[]{typeof(string),typeof(string),typeof(string)})
                .Invoke(null,new object[]{"Keyboard","V11_InteractionRegressionKeyboard",null});
            Assert.IsNotNull(keyboard);keyboard.GetType().GetMethod("MakeCurrent",Type.EmptyTypes).Invoke(keyboard,null);
            KeyboardState(false);
        }
        private static void TryCleanup(Action action,ref Exception failure)
        {
            try {action();}
            catch(Exception error) {failure=failure==null?error:new AggregateException(failure,error);}
        }
        [UnityTearDown] public IEnumerator Close()
        {
            // Stop the interactor BEFORE releasing the test keyboard. The accepted
            // fixture restores the original inventory/prefs/context even on failure.
            Exception failure=null;
            TryCleanup(()=>{if(Player)((Behaviour)Interactor).enabled=false;},ref failure);
            TryCleanup(()=>{if(keyboard!=null)KeyboardState(false);},ref failure);
            TryCleanup(()=>{
                if(keyboard!=null)
                    InputType("InputSystem").GetMethod("RemoveDevice",new[]{InputType("InputDevice")}).Invoke(null,new[]{keyboard});
            },ref failure);
            keyboard=null;
            TryCleanup(()=>{
                if(previousKeyboard!=null && (bool)Get(previousKeyboard,"added"))
                    previousKeyboard.GetType().GetMethod("MakeCurrent",Type.EmptyTypes).Invoke(previousKeyboard,null);
            },ref failure);
            previousKeyboard=null;
            TryCleanup(()=>{if(inputSettings!=null){
                inputSettings.GetType().GetProperty("editorInputBehaviorInPlayMode").SetValue(inputSettings,previousEditorInput);
                inputSettings.GetType().GetProperty("backgroundBehavior").SetValue(inputSettings,previousBackground);
            }},ref failure);
            TryCleanup(()=>PlayerCursor.EndSimulation(),ref failure);
            foreach(GameObject item in temporary)
                TryCleanup(()=>{if(item){item.SetActive(false);Object.Destroy(item);}},ref failure);
            temporary.Clear();yield return null;
            if(scope!=null)yield return scope.Close();
            if(failure!=null)throw new InvalidOperationException("Interaction fixture cleanup failed.",failure);
        }

        [UnityTest] public IEnumerator FirstPersonKeepsItsTwoMetreRayAndOnceOnlyFCollection()
        {
            Component fuse=Part("fuse");Aim(fuse.GetComponent<Collider>(),false,3f);
            Assert.IsNull(Call(Interactor,"FindTarget"));Press();Assert.AreEqual(0,Quantity("fuse"));
            Aim(fuse.GetComponent<Collider>(),false);Assert.AreSame(fuse,Call(Interactor,"FindTarget"));
            Press();Assert.IsFalse(fuse.gameObject.activeSelf);Assert.AreEqual(1,Quantity("fuse"));
            Assert.AreEqual(1,Get(C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3TreasureProgress"),"CollectedCount"));
            Press();Assert.AreEqual(1,Quantity("fuse"));yield return null;
        }

        [UnityTest] public IEnumerator ThirdPersonUsesBodyDistanceForAllThreeRealQuestParts()
        {
            int expected=0;
            foreach(string id in new[]{"fuse","control_chip","filter_module"})
            {
                Component part=Part(id);Collider collider=part.GetComponent<Collider>();
                Aim(collider,true,3f);Assert.IsNull(Call(Interactor,"FindTarget"));Press();Assert.AreEqual(0,Quantity(id));
                Aim(collider,true);Assert.Greater(Vector3.Distance(Camera.transform.position,collider.bounds.center),2f);
                Camera.transform.rotation=Quaternion.LookRotation(Vector3.left);Assert.IsNull(Call(Interactor,"FindTarget"));Press();Assert.AreEqual(0,Quantity(id));
                Camera.transform.rotation=Quaternion.LookRotation(collider.bounds.center-Camera.transform.position);
                Assert.AreSame(part,Call(Interactor,"FindTarget"));Press(expected%2==0);
                Assert.IsFalse(part.gameObject.activeSelf);Assert.AreEqual(1,Quantity(id));expected++;
                Assert.AreEqual(expected,Get(C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3TreasureProgress"),"CollectedCount"));
                Press();Assert.AreEqual(1,Quantity(id));yield return null;
            }
        }

        [UnityTest] public IEnumerator ThirdPersonRejectsCameraWallsBodyWallsAndFloors()
        {
            Component fuse=Part("fuse");Collider target=fuse.GetComponent<Collider>();
            for(int kind=0;kind<3;kind++)
            {
                Aim(target,true,1.3f,kind>0);
                Vector3 reach=(Vector3)Get(Interactor,"ReachOrigin");
                if(kind==2)
                {
                    target.transform.position=reach+new Vector3(0,1.5f,1f);Physics.SyncTransforms();
                    Camera.transform.position=reach+new Vector3(.42f,3f,-3f);
                    Camera.transform.rotation=Quaternion.LookRotation(target.bounds.center-Camera.transform.position);
                }
                GameObject wall=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(wall);
                wall.name="V11_TEST_REACH_OBSTRUCTION_"+kind;
                wall.transform.position=kind==0?Vector3.Lerp(Camera.transform.position,target.bounds.center,.35f):
                    kind==1?reach+Vector3.forward*.6f:reach+new Vector3(0,.7f,.5f);
                wall.transform.localScale=kind==0?new Vector3(2,2,.2f):kind==1?new Vector3(2,.35f,.12f):new Vector3(3,.1f,3);
                Physics.SyncTransforms();
                if(kind>0)
                {
                    Assert.IsTrue(Physics.Raycast(Camera.transform.position,Camera.transform.forward,out RaycastHit visible,7.5f,~0,QueryTriggerInteraction.Ignore));
                    Assert.AreSame(target,visible.collider,"Camera must see the item while the BODY path is blocked.");
                }
                Assert.IsNull(Call(Interactor,"FindTarget"));Press();Assert.IsTrue(fuse.gameObject.activeSelf);Assert.AreEqual(0,Quantity("fuse"));
                wall.SetActive(false);Physics.SyncTransforms();yield return null;
            }
            Aim(target,true);Assert.AreSame(fuse,Call(Interactor,"FindTarget"));Press();Assert.AreEqual(1,Quantity("fuse"));
        }

        [UnityTest] public IEnumerator UiContextAndBothUpdateOrdersNeverDispatchTwoInteractions()
        {
            Component fuse=Part("fuse");Aim(fuse.GetComponent<Collider>(),true);
            Context("UI");Press();Assert.AreEqual(0,Quantity("fuse"));Assert.IsTrue(fuse.gameObject.activeSelf);
            Context("Gameplay");Call(Game,"ToggleInventory");Press();
            Assert.IsTrue((bool)Get(Game,"inventoryOpen"));Assert.AreEqual(0,Quantity("fuse"));
            Call(Game,"ToggleInventory");Assert.IsFalse((bool)Get(Game,"inventoryOpen"));
            GameObject cache=GameObject.Find("Hidden_ElectronicScrapCache_F");
            // A real cache just behind the quest item would become the next
            // ray hit if a second handler ran after collection disables it.
            cache.transform.position=Parking+new Vector3(0,.6f,2.05f);
            foreach(string id in new[]{"fuse","control_chip"})
            {
                Component part=Part(id);Aim(part.GetComponent<Collider>(),true);Assert.AreSame(part,Call(Interactor,"FindTarget"));
                Press(id=="fuse");Assert.AreEqual(1,Quantity(id));Assert.AreEqual(0,Quantity("eco_recovered_circuit"));
                Assert.IsTrue(cache.activeSelf,"Unpowered cache cannot be collected.");
                StringAssert.Contains("已取得",(string)Get(Game,"message"),"A second workshop dispatch overwrote the quest pickup feedback.");
                yield return null;
            }
        }

        [UnityTest] public IEnumerator ThirdPersonMaterialsKeepActivationSortingAndOnceOnlyRecovery()
        {
            GameObject circuit=GameObject.Find("Hidden_ElectronicScrapCache_F");Aim(circuit.GetComponent<Collider>(),true);
            Press();Assert.IsTrue(circuit.activeSelf);Assert.AreEqual(0,Quantity("eco_recovered_circuit"));
            // The negative cache check left this collider at the shared test target.
            // Park it until the main-quest parts are collected, then Aim() restores it.
            circuit.transform.position=Parking+Vector3.right*20f;
            Physics.SyncTransforms();
            foreach(string id in new[]{"fuse","control_chip","filter_module"})
            {Component part=Part(id);Aim(part.GetComponent<Collider>(),true);Press();Assert.AreEqual(1,Quantity(id));yield return null;}
            Type step=TypeOf("C3TreasurePuzzle.C3ProcessingStep");Array answer=Array.CreateInstance(step,3);
            string[] order={"Sorting","Recycling","PollutionTreatment"};for(int i=0;i<3;i++)answer.SetValue(Enum.Parse(step,order[i]),i);
            Assert.IsTrue((bool)Call(C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3PuzzleState"),"SubmitAnswer",answer));
            Component device=C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3DeviceState");
            Assert.IsTrue((bool)Call(device,"TryActivate"));Call(device,"NotifyStartupFinished");
            for(int kind=0;kind<2;kind++)
            {
                Collider target=circuit.GetComponent<Collider>();Aim(target,true,1.3f,kind==1);
                GameObject wall=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(wall);
                wall.name="V11_TEST_MATERIAL_OBSTRUCTION_"+kind;
                wall.transform.position=kind==0?Vector3.Lerp(Camera.transform.position,target.bounds.center,.3f):
                    (Vector3)Get(Interactor,"ReachOrigin")+Vector3.forward*.6f;
                wall.transform.localScale=kind==0?new Vector3(2f,2f,.2f):new Vector3(2f,.5f,.12f);
                Physics.SyncTransforms();
                if(kind==1)
                {
                    Assert.IsTrue(Physics.Raycast(Camera.transform.position,Camera.transform.forward,out RaycastHit visible,8f,~0,QueryTriggerInteraction.Ignore));
                    Assert.AreSame(target,visible.collider,"Camera sees the cache but body reach is obstructed.");
                }
                Press();Assert.IsTrue(circuit.activeSelf);Assert.AreEqual(0,Quantity("eco_recovered_circuit"));
                wall.SetActive(false);Physics.SyncTransforms();yield return null;
            }
            Aim(circuit.GetComponent<Collider>(),true);Press();Assert.IsFalse(circuit.activeSelf);Assert.AreEqual(1,Quantity("eco_recovered_circuit"));
            string[] names={"Distributed_RecycledPlastic_F","Distributed_RecycledMetal_F"};
            string[] ids={"eco_recycled_plastic","eco_recycled_metal"},kinds={"Plastic","Metal"};
            for(int i=0;i<2;i++)
            {
                GameObject cache=GameObject.Find(names[i]);Aim(cache.GetComponent<Collider>(),true);Press();
                Assert.IsTrue(cache.activeSelf);Assert.AreEqual(0,Quantity(ids[i]),"Sorting prerequisite was bypassed.");
                Type kind=TypeOf("EcoRecycleCargo+Kind");
                Assert.IsTrue((bool)Call(Game,"TryCommitRecyclingBatch","tp-"+kinds[i],Enum.Parse(kind,kinds[i]),
                    new[]{"tp-"+kinds[i]+"-0","tp-"+kinds[i]+"-1","tp-"+kinds[i]+"-2"}));
                Press();Assert.IsFalse(cache.activeSelf);Assert.AreEqual(1,Quantity(ids[i]));Press();Assert.AreEqual(1,Quantity(ids[i]));
                yield return null;
            }
            Assert.AreEqual(1,Quantity("eco_recovered_circuit"));Assert.AreEqual(6,Get(Game,"RecyclingReceiptCount"));
        }
    }
}
