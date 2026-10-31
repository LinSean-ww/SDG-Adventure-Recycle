using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C3TreasurePuzzle
{
    public enum C3MaterialState
    { Spawned,Feeding,MainBelt,Identified,Diverting,MachineInput,Processing,OutputReady,Outfeeding,Collected }

    [DisallowMultipleComponent]
    public sealed class C3RecyclingLine : MonoBehaviour
    {
        public const int UnitsPerBale=3;
        public const int BalesPerMachineCapacity=3;
        public const float TransportSpeed=4.5f;
        public const float ProcessingSeconds=2.5f;
        public const float MinimumSpacing=1f;
        [Tooltip("Finite C-side demonstration supply, NOT completion of the unavailable B level.")]
        public bool useFiniteTestSupply=true;

        public sealed class MaterialRecord
        {
            public string MaterialId { get; internal set; }
            public EcoRecycleCargo.Kind MaterialType { get; internal set; }
            public C3MaterialState State { get; internal set; }
            public int CurrentRouteSegment { get; internal set; }
            public int TargetMachine { get; internal set; }
            public bool Counted { get; internal set; }
            public bool ProcessingComplete { get; internal set; }
            public bool ProductCreated { get; internal set; }
            public string BatchId { get; internal set; }
            public EcoRecycleCargo Cargo { get; internal set; }
            internal Transform Visual;
            internal Vector3 VisualScale;
            public int Units => 1;
            public readonly List<C3MaterialState> History=new List<C3MaterialState>();
            internal TransportPath Path;
            internal float Distance;
            internal int Slot=-1;
        }
        public sealed class FinishedBatch
        {
            public string BatchId { get; internal set; }
            public EcoRecycleCargo.Kind MaterialType { get; internal set; }
            public string[] SourceMaterialIds { get; internal set; }
            public Transform Product { get; internal set; }
            public bool OutputReached { get; internal set; }
            public bool Collected { get; internal set; }
            public int CurrentRouteSegment { get; internal set; }
            internal TransportPath Path;
            internal float Distance;
            internal float OutputDistance;
        }
        internal sealed class Leg
        {
            public Vector3 A,B;
            public Collider Support;
            public Vector3 Normal => Support.transform.up;
            public float Length => Vector3.Distance(A,B);
        }
        internal sealed class TransportPath
        {
            public readonly List<Leg> Legs=new List<Leg>();
            public Vector3 EntranceNormal=Vector3.up,ExitNormal=Vector3.up;
            public Vector3 ExitForward;
            public float Length => Legs.Sum(s=>s.Length);
            public void Add(C3ConveyorSection section) => Legs.Add(new Leg {
                A=section.Start.position,B=section.End.position,Support=section.Surface });
            public void Place(Transform item,float distance,float radius,Vector3 boxSize,out int segment)
            {
                Evaluate(distance,radius,boxSize,out Vector3 position,out Quaternion rotation,out segment);
                item.SetPositionAndRotation(position,rotation);
            }
            public float Move(Transform item,float from,float to,float maxMovement,float radius,Vector3 boxSize,out int segment)
            {
                Evaluate(to,radius,boxSize,out Vector3 position,out Quaternion rotation,out segment);
                if (to>from && Vector3.Distance(item.position,position)>maxMovement+.00001f)
                {
                    // A sphere rolling around a steep hopper lip follows a longer
                    // physical arc than the centreline parameter. Bound world travel
                    // by speed*dt instead of snapping across that extra distance.
                    float low=from,high=to;
                    for (int i=0;i<10;i++)
                    {
                        float mid=(low+high)*.5f;
                        Evaluate(mid,radius,boxSize,out position,out rotation,out segment);
                        if (Vector3.Distance(item.position,position)<=maxMovement) low=mid; else high=mid;
                    }
                    to=low;
                    Evaluate(to,radius,boxSize,out position,out rotation,out segment);
                }
                item.SetPositionAndRotation(position,rotation);
                return to;
            }
            internal void Evaluate(float distance,float radius,Vector3 boxSize,out Vector3 position,out Quaternion rotation,out int segment)
            {
                segment=0;
                while (segment<Legs.Count-1 && distance>Legs[segment].Length)
                { distance-=Legs[segment].Length; segment++; }
                Leg leg=Legs[segment];
                float t=leg.Length>.0001f?Mathf.Clamp01(distance/leg.Length):1f;
                Vector3 previous=segment>0?Legs[segment-1].Normal:EntranceNormal;
                Vector3 next=segment<Legs.Count-1?Legs[segment+1].Normal:ExitNormal;
                Vector3 n0=(previous+leg.Normal).normalized,n1=(next+leg.Normal).normalized;
                float blend=Mathf.Max(.001f,Mathf.Min(.35f,leg.Length*.5f));
                float enter=Mathf.Clamp01(t*leg.Length/blend),leave=Mathf.Clamp01(1f-(1f-t)*leg.Length/blend);
                Vector3 up=Vector3.Slerp(Vector3.Slerp(n0,leg.Normal,enter),n1,leave).normalized;
                Vector3 direction=(leg.B-leg.A).normalized;
                Vector3 previousDirection=segment>0?(Legs[segment-1].B-Legs[segment-1].A).normalized:direction;
                Vector3 nextDirection=segment<Legs.Count-1?(Legs[segment+1].B-Legs[segment+1].A).normalized:
                    (ExitForward.sqrMagnitude>.1f?ExitForward:direction);
                Vector3 forward0=(previousDirection+direction).normalized;
                Vector3 forward1=segment==Legs.Count-1 && ExitForward.sqrMagnitude>.1f?ExitForward:(direction+nextDirection).normalized;
                Vector3 heading=Vector3.Slerp(Vector3.Slerp(forward0,direction,enter),forward1,leave);
                Vector3 forward=Vector3.ProjectOnPlane(heading,up).normalized;
                if (forward.sqrMagnitude<.1f) forward=Vector3.forward;
                rotation=Quaternion.LookRotation(forward,up);
                float extent=radius;
                if (boxSize!=Vector3.zero)
                    extent=(Mathf.Abs(Vector3.Dot(rotation*Vector3.right,leg.Normal))*boxSize.x+
                        Mathf.Abs(Vector3.Dot(up,leg.Normal))*boxSize.y+
                        Mathf.Abs(Vector3.Dot(rotation*Vector3.forward,leg.Normal))*boxSize.z)*.5f;
                // Support distance is measured along the actual belt normal. At
                // slope joints the shared bisector gives the same end/start pose.
                Vector3 offset=up*(extent/Mathf.Max(.15f,Vector3.Dot(up,leg.Normal)));
                position=Vector3.Lerp(leg.A,leg.B,t)+offset;
                // Cast from above the actual neighbouring finite surfaces. This
                // handles both spheres bridging a steep hopper lip and bales
                // whose infinite-plane contact corner lies beyond a belt end.
                int first=Mathf.Max(0,segment-1),last=Mathf.Min(Legs.Count-1,segment+1);
                float margin=radius+boxSize.magnitude*.5f+.2f;
                float startY=position.y+margin,lowest=position.y;
                for (int i=first;i<=last;i++)
                {
                    startY=Mathf.Max(startY,Legs[i].Support.bounds.max.y+margin);
                    lowest=Mathf.Min(lowest,Legs[i].Support.bounds.min.y);
                }
                Vector3 origin=new Vector3(position.x,startY,position.z);
                RaycastHit[] hits=boxSize!=Vector3.zero
                    ?Physics.BoxCastAll(origin,boxSize*.5f,Vector3.down,rotation,startY-lowest+margin,~0,QueryTriggerInteraction.Ignore)
                    :Physics.SphereCastAll(origin,radius,Vector3.down,startY-lowest+margin,~0,QueryTriggerInteraction.Ignore);
                float drop=float.PositiveInfinity;
                foreach (RaycastHit hit in hits)
                    for (int i=first;i<=last;i++)
                        if (Legs[i].Support==hit.collider) drop=Mathf.Min(drop,hit.distance);
                if (float.IsPositiveInfinity(drop))
                    throw new InvalidOperationException("Material lost its physical conveyor: "+leg.Support.name);
                position.y=startY-drop+.001f;
            }
        }
        internal sealed class MachineWork
        {
            public readonly MaterialRecord[] Slots=new MaterialRecord[UnitsPerBale];
            public bool Processing;
            public float Elapsed;
            public int Completed;
            public FinishedBatch Prepared;
        }
        public sealed class RecyclingSession
        {
            public string SessionId { get; private set; }
            public bool IsPresentation { get; private set; }
            public Transform ObjectRoot { get; internal set; }
            public IReadOnlyList<MaterialRecord> Materials => Records;
            public IReadOnlyList<FinishedBatch> Products => Batches;
            public float ElapsedSeconds { get; internal set; }
            public int ReceiptsCommitted { get; internal set; }
            public int MaximumObjectCount { get; internal set; }
            internal readonly List<MaterialRecord> Records;
            internal readonly List<FinishedBatch> Batches;
            internal readonly HashSet<string> Accepted;
            internal readonly MachineWork[] Work;
            internal float ReleaseClock;
            internal RecyclingSession(string id,bool presentation,Transform root,List<MaterialRecord> input=null,
                List<FinishedBatch> output=null,HashSet<string> ids=null,MachineWork[] machines=null)
            {
                SessionId=id; IsPresentation=presentation; ObjectRoot=root;
                Records=input??new List<MaterialRecord>(); Batches=output??new List<FinishedBatch>();
                Accepted=ids??new HashSet<string>(StringComparer.Ordinal);
                Work=machines??new[] {new MachineWork(),new MachineWork(),new MachineWork()};
            }
        }
        public enum PresentationState { None,Running,Completed,Cancelled,Failed }
        public sealed class PresentedMaterial
        {
            public string MaterialId { get; internal set; }
            public EcoRecycleCargo.Kind MaterialType { get; internal set; }
            public int ObjectId { get; internal set; }
            public int TargetMachine { get; internal set; }
            public C3MaterialState[] History { get; internal set; }
            public bool ProcessingComplete { get; internal set; }
            public bool Counted { get; internal set; }
        }
        public sealed class PresentedProduct
        {
            public string BatchId { get; internal set; }
            public EcoRecycleCargo.Kind MaterialType { get; internal set; }
            public string[] SourceMaterialIds { get; internal set; }
            public int ObjectId { get; internal set; }
            public Vector3 Position { get; internal set; }
            public Quaternion Rotation { get; internal set; }
            public bool Collected { get; internal set; }
            public bool OutputReached { get; internal set; }
        }
        public sealed class PresentationResult
        {
            public string SessionId { get; internal set; }
            public PresentationState State { get; internal set; }
            public PresentedMaterial[] Materials { get; internal set; }
            public PresentedProduct[] Products { get; internal set; }
            public int ReceiptsCommitted { get; internal set; }
            public int MaximumObjectCount { get; internal set; }
            public float ElapsedSeconds { get; internal set; }
            public string Failure { get; internal set; }
        }
        private sealed class FormalVisibility
        {
            public GameObject Object;
            public bool Active;
        }
        private C3DeviceState device;
        private C3RecyclingLineGeometry geometry;
        private C3RecyclingVisuals visuals;
        private Transform cargoRoot;
        private readonly List<MaterialRecord> records=new List<MaterialRecord>();
        private readonly List<FinishedBatch> batches=new List<FinishedBatch>();
        private readonly HashSet<string> accepted=new HashSet<string>(StringComparer.Ordinal);
        private readonly MachineWork[] work={new MachineWork(),new MachineWork(),new MachineWork()};
        private bool supplied;
        private string runId;
        private RecyclingSession official,presentation;
        private readonly List<FormalVisibility> formalVisibility=new List<FormalVisibility>();
        public RecyclingSession ActivePresentation => presentation;
        public PresentationResult LastPresentation { get; private set; }
        public PresentationState PresentationStatus => presentation!=null?PresentationState.Running:
            LastPresentation!=null?LastPresentation.State:PresentationState.None;
        public bool FirstProductionCollected => records.Count==9 && batches.Count==3 &&
            records.All(r=>r.ProcessingComplete && r.Counted && r.State==C3MaterialState.Collected) && batches.All(b=>b.Collected);
        public bool CanStartPresentation => isActiveAndEnabled && device && device.FactoryState==C3FactoryState.Running &&
            FirstProductionCollected && presentation==null && !C3CompletionScreen.BlocksGameplayInputThisFrame;
        public string PresentationProgress
        {
            get
            {
                if (presentation!=null) return "展示進行中：進料 "+presentation.Records.Count(r=>r.State!=C3MaterialState.Spawned)+
                    "/9，加工 "+presentation.Batches.Count+"/3，收集 "+presentation.Batches.Count(b=>b.Collected)+"/3。";
                if (!device || device.FactoryState!=C3FactoryState.Running) return "工廠尚未進入運作狀態。";
                if (!FirstProductionCollected) return "請等待第一次正式加工及三件成品收集完成。";
                if (LastPresentation!=null && LastPresentation.State==PresentationState.Failed) return "展示已停止，可再次播放。";
                return LastPresentation!=null && LastPresentation.State==PresentationState.Completed?"展示已完成，可再次播放。":"可開始產線展示。";
            }
        }
        private EcoRecycleCargo.Kind lastCompleted=EcoRecycleCargo.Kind.Plastic;
        // Envelope includes the visible bale straps; body is slightly inset.
        public static readonly Vector3 ProductSize=new Vector3(.52f,.43f,.51f);
        public event Action<string> BatchCompleted;
        public string LastBlockedReason { get; private set; }="";
        public IReadOnlyList<MaterialRecord> Materials => records;
        public IReadOnlyList<FinishedBatch> Products => batches;
        public int AcceptedUnits => records.Count;
        public int FedUnits => records.Count(r=>r.State!=C3MaterialState.Spawned);
        public int WaitingUnits => records.Count(r=>r.State<C3MaterialState.Processing);
        public int ProcessingUnits => records.Count(r=>r.State==C3MaterialState.Processing);
        public int ProcessedUnits => records.Count(r=>r.ProcessingComplete);
        public int ProcessedBatches => batches.Count;
        public int OutputProducts => batches.Count(b=>b.OutputReached);
        public int CollectedProducts => batches.Count(b=>b.Collected);
        public int LastCompletedLegacyKind => LegacyKind(lastCompleted);
        public bool IsOperational => device && device.FactoryState>=C3FactoryState.Running;
        public string SupplyProvenance => "Finite C-side test supply; B level is not connected";

        public static int MachineIndex(EcoRecycleCargo.Kind kind)
        {
            switch (kind) { case EcoRecycleCargo.Kind.Paper:return 0;
                case EcoRecycleCargo.Kind.Metal:return 1; case EcoRecycleCargo.Kind.Plastic:return 2; default:return -1; }
        }
        public static int LegacyKind(EcoRecycleCargo.Kind kind)
        {
            switch (kind) { case EcoRecycleCargo.Kind.Paper:return 2;
                case EcoRecycleCargo.Kind.Metal:return 1; case EcoRecycleCargo.Kind.Plastic:return 0; default:return -1; }
        }
        private void Awake()
        {
            device=GetComponent<C3DeviceState>(); geometry=GetComponent<C3RecyclingLineGeometry>();
            if (!device || !geometry || !geometry.BuildRoot) throw new InvalidOperationException("Recycling line requires device and built geometry");
            visuals=geometry.Visuals;
            if (!visuals) throw new InvalidOperationException("Recycling visual library is not ready");
            runId=Guid.NewGuid().ToString("N");
            cargoRoot=new GameObject("P2_TRACKED_MATERIALS").transform;
            cargoRoot.SetParent(geometry.BuildRoot,false);
            official=new RecyclingSession(runId,false,cargoRoot,records,batches,accepted,work);
        }
        private void OnEnable() { if (device) device.Completed+=CancelPresentation; }
        private void OnDisable()
        {
            if (device) device.Completed-=CancelPresentation;
            CancelPresentation();
        }
        private bool CanAccept(string id,EcoRecycleCargo.Kind kind)
        {
            EcoAdventureRuntime runtime=EcoAdventureRuntime.Instance;
            int machine=MachineIndex(kind);
            return gameObject.activeInHierarchy && presentation==null && device.FactoryState!=C3FactoryState.Completed &&
                runtime && machine>=0 && !string.IsNullOrWhiteSpace(id) && id.Length<=128 &&
                !accepted.Contains(id) && !runtime.HasRecyclingReceipt(id) &&
                records.Count(r=>r.TargetMachine==machine)<UnitsPerBale*BalesPerMachineCapacity &&
                runtime.RecyclingReceiptCount+records.Count(r=>!r.Counted)<EcoAdventureRuntime.RecyclingReceiptCapacity;
        }
        public bool TrySupply(string id,EcoRecycleCargo.Kind kind)
        {
            if (!CanAccept(id,kind)) return false;
            CreateCargo(official,id,kind); return true;
        }
        private void CreateCargo(RecyclingSession session,string id,EcoRecycleCargo.Kind kind)
        {
            GameObject item=new GameObject((session.IsPresentation?"P22_RAW_":"P2_RAW_")+id);
            item.transform.SetParent(session.ObjectRoot,false);
            item.transform.position=geometry.BMaterialInterface.position+Vector3.up*C3RecyclingLineGeometry.RawRadius;
            try
            {
                visuals.CreateRaw(kind,session.Records.Count(r=>r.MaterialType==kind)%C3RecyclingVisuals.VariantsPerMaterial,item.transform);
                EcoRecycleCargo cargo=item.AddComponent<EcoRecycleCargo>();
                cargo.materialId=id; cargo.materialType=kind;
                Admit(cargo,session);
            }
            catch { item.SetActive(false); Destroy(item); throw; }
        }
        public bool TryAcceptCargo(EcoRecycleCargo cargo)
        {
            if (!cargo || cargo.processed || cargo.lineOwned || cargo.materialUnits!=1 ||
                cargo.gameObject.scene!=gameObject.scene || !CanAccept(cargo.materialId,cargo.materialType)) return false;
            if (Vector3.Distance(cargo.transform.position,geometry.BMaterialInterface.position+
                Vector3.up*C3RecyclingLineGeometry.RawRadius)>.15f) return false;
            // Transform transport contract: upstream retains rejected objects.
            // Never silently disable an unknown solid collider or rigidbody.
            if (cargo.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled && !c.isTrigger) ||
                cargo.GetComponentsInChildren<Rigidbody>(true).Any(b=>!b.isKinematic) ||
                Vector3.Distance(cargo.transform.lossyScale,Vector3.one)>.001f) return false;
            Renderer[] renderers=cargo.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length==0 || renderers.Any(r=>r.bounds.size.magnitude>.43f) || !C3RecyclingVisuals.FitsRawEnvelope(cargo.transform)) return false;
            Admit(cargo,official); return true;
        }
        private void Admit(EcoRecycleCargo cargo,RecyclingSession session)
        {
            if (!session.Accepted.Add(cargo.materialId)) throw new InvalidOperationException("Duplicate session material ID");
            cargo.lineOwned=true;
            cargo.transform.SetParent(session.ObjectRoot,true);
            MaterialRecord record=new MaterialRecord {MaterialId=cargo.materialId,MaterialType=cargo.materialType,
                TargetMachine=MachineIndex(cargo.materialType),Cargo=cargo,State=C3MaterialState.Spawned};
            record.Visual=C3RecyclingVisuals.UnifyRawVisualRoot(cargo.transform);
            record.VisualScale=record.Visual.localScale;
            record.History.Add(record.State);
            record.Path=new TransportPath {ExitNormal=geometry.Machines[record.TargetMachine].Chute.Normal};
            foreach (C3ConveyorSection section in geometry.CommonSections) record.Path.Add(section);
            record.Path.Legs.Add(new Leg {A=geometry.MainBelt.Start.position,
                B=geometry.Machines[record.TargetMachine].Chute.Start.position,Support=geometry.MainBelt.Surface});
            record.Cargo.gameObject.SetActive(false);
            session.Records.Add(record);
        }
        private static void Transition(MaterialRecord record,C3MaterialState next)
        {
            if ((int)next!=(int)record.State+1) throw new InvalidOperationException("Invalid material transition "+record.State+" -> "+next);
            record.State=next; record.History.Add(next);
        }
        private void Update() => AdvanceSimulation(Time.deltaTime);
        public void AdvanceSimulation(float dt)
        {
            if (!gameObject.activeInHierarchy || !IsOperational || Time.timeScale<=0f || dt<=0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            // Bound integration even after a long frame; never jump through stages.
            dt=Mathf.Min(dt,.25f);
            if (!supplied && useFiniteTestSupply && device.FactoryState==C3FactoryState.Running)
            {
                supplied=true;
                for (int i=0;i<9;i++) TrySupply("DEMO-"+runId+"-"+i,
                    i%3==0?EcoRecycleCargo.Kind.Plastic:i%3==1?EcoRecycleCargo.Kind.Metal:EcoRecycleCargo.Kind.Paper);
            }
            if (presentation!=null)
            {
                if (device.FactoryState!=C3FactoryState.Running) { CancelPresentation(); return; }
                try
                {
                    TickSession(presentation,dt);
                    if (presentation.Batches.Count==3 && presentation.Batches.All(b=>b.Collected))
                        FinishPresentation(PresentationState.Completed,"");
                }
                catch (Exception error)
                {
                    FinishPresentation(PresentationState.Failed,error.Message);
                    Debug.LogException(error,this);
                }
            }
            else TickSession(official,dt);
        }
        private void TickSession(RecyclingSession session,float dt)
        {
            List<MaterialRecord> records=session.Records; MachineWork[] work=session.Work;
            session.ElapsedSeconds+=dt; session.ReleaseClock+=dt;
            MaterialRecord waiting=records.FirstOrDefault(r=>r.State==C3MaterialState.Spawned);
            if (waiting!=null && session.ReleaseClock>=.45f && records.Where(OnPrimary).All(r=>r.Distance>MinimumSpacing))
            {
                session.ReleaseClock=0f; Transition(waiting,C3MaterialState.Feeding);
                waiting.Cargo.gameObject.SetActive(true); PlaceRaw(waiting);
            }
            MaterialRecord[] alreadyDiverting=records.Where(r=>r.State==C3MaterialState.Diverting).ToArray();
            foreach (MaterialRecord record in records.Where(OnPrimary).OrderByDescending(r=>r.Distance).ToArray())
            {
                if (record.State==C3MaterialState.Identified) { TryDivert(record,session); continue; }
                float next=Mathf.Min(record.Path.Length,record.Distance+TransportSpeed*dt);
                foreach (MaterialRecord ahead in records.Where(OnPrimary))
                    if (ahead!=record && ahead.Distance>record.Distance+.001f) next=Mathf.Min(next,ahead.Distance-MinimumSpacing);
                MoveRaw(record,Mathf.Max(record.Distance,next),TransportSpeed*dt);
                float commonLength=record.Path.Legs.Take(geometry.CommonSections.Count).Sum(s=>s.Length);
                if (record.State==C3MaterialState.Feeding && record.Distance>=commonLength) Transition(record,C3MaterialState.MainBelt);
                if (record.Distance>=record.Path.Length-.0001f)
                {
                    if (record.State==C3MaterialState.Feeding) Transition(record,C3MaterialState.MainBelt);
                    Transition(record,C3MaterialState.Identified); TryDivert(record,session);
                }
            }
            foreach (MaterialRecord record in alreadyDiverting)
            {
                MoveRaw(record,Mathf.Min(record.Path.Length,record.Distance+TransportSpeed*dt),TransportSpeed*dt);
                if (record.Distance>=record.Path.Length-.0001f) Transition(record,C3MaterialState.MachineInput);
            }
            for (int lane=0;lane<work.Length;lane++)
            {
                MaterialRecord atGate=records.FirstOrDefault(r=>r.TargetMachine==lane &&
                    r.State==C3MaterialState.Diverting && r.Distance<geometry.Machines[lane].Chute.Length);
                geometry.SetDiverter(lane,atGate!=null?atGate.Distance/geometry.Machines[lane].Chute.Length:0f,atGate!=null,dt);
            }
            TickProducts(session,dt);
            TickMachines(session,dt);
            geometry.AnimateRollers(dt);
        }
        private static bool OnPrimary(MaterialRecord record) => record.State==C3MaterialState.Feeding ||
            record.State==C3MaterialState.MainBelt || record.State==C3MaterialState.Identified;
        private void PlaceRaw(MaterialRecord record)
        {
            int segment;
            record.Path.Place(record.Cargo.transform,record.Distance,C3RecyclingLineGeometry.RawRadius,Vector3.zero,out segment);
            record.CurrentRouteSegment=segment;
        }
        private void MoveRaw(MaterialRecord record,float next,float maxMovement)
        {
            int segment;
            record.Distance=record.Path.Move(record.Cargo.transform,record.Distance,next,maxMovement,
                C3RecyclingLineGeometry.RawRadius,Vector3.zero,out segment);
            record.CurrentRouteSegment=segment;
        }
        private void TryDivert(MaterialRecord record,RecyclingSession session)
        {
            MachineWork machine=session.Work[record.TargetMachine];
            int slot=Array.FindIndex(machine.Slots,r=>r==null);
            if (slot<0) return;
            machine.Slots[slot]=record; record.Slot=slot;
            C3RecyclingLineGeometry.Machine model=geometry.Machines[record.TargetMachine];
            record.Path=new TransportPath {EntranceNormal=geometry.MainBelt.Normal};
            foreach (C3ConveyorSection section in model.InputSections) record.Path.Add(section);
            record.Path.Legs.Add(new Leg {A=model.ReceivingBed.End.position,
                B=model.Tray.position+new Vector3((slot-1)*.32f,0f,-.2f),Support=model.ProcessBed});
            record.Distance=0f; Transition(record,C3MaterialState.Diverting); PlaceRaw(record);
        }
        private void TickMachines(RecyclingSession session,float dt)
        {
            MachineWork[] work=session.Work;
            for (int lane=0;lane<work.Length;lane++)
            {
                MachineWork machine=work[lane];
                if (!machine.Processing && machine.Slots.All(r=>r!=null && r.State==C3MaterialState.MachineInput))
                {
                    if (machine.Slots.Any(r=>r.TargetMachine!=lane || MachineIndex(r.MaterialType)!=lane))
                        throw new InvalidOperationException("Wrong material in processing machine "+lane);
                    machine.Processing=true; machine.Elapsed=0f;
                    foreach (MaterialRecord record in machine.Slots) Transition(record,C3MaterialState.Processing);
                }
                if (!machine.Processing) continue;
                machine.Elapsed=Mathf.Min(ProcessingSeconds,machine.Elapsed+dt);
                float t=machine.Elapsed/ProcessingSeconds;
                geometry.SetPress(lane,Mathf.Sin(Mathf.PI*t));
                foreach (MaterialRecord record in machine.Slots)
                {
                    float squash=Mathf.Lerp(1f,.4f,Mathf.Clamp01(t*2f));
                    record.Visual.localScale=Vector3.Scale(record.VisualScale,new Vector3(1f,squash,1f));
                    record.Cargo.transform.rotation=Quaternion.identity;
                    record.Cargo.transform.position=geometry.Machines[lane].Tray.position+
                        new Vector3((record.Slot-1)*.32f,C3RecyclingLineGeometry.RawRadius*squash,-.2f);
                }
                if (machine.Elapsed<ProcessingSeconds) continue;
                if (machine.Prepared==null) machine.Prepared=PrepareProduct(lane,machine,session);
                FinishedBatch batch=machine.Prepared;
                if (!CommitBatch(session,batch))
                {
                    LastBlockedReason="Processing receipt rejected; no output or duplicate credit emitted: "+batch.BatchId;
                    continue;
                }
                foreach (MaterialRecord record in machine.Slots)
                {
                    record.Counted=!session.IsPresentation;
                    record.ProcessingComplete=record.ProductCreated=true;
                    record.BatchId=batch.BatchId; record.Cargo.processed=true;
                    record.CurrentRouteSegment=0;
                    Transition(record,C3MaterialState.OutputReady);
                    record.Cargo.gameObject.SetActive(false);
                }
                batch.Product.gameObject.SetActive(true); session.Batches.Add(batch);
                if (!session.IsPresentation) lastCompleted=batch.MaterialType;
                machine.Completed++;
                machine.Processing=false; machine.Elapsed=0f; machine.Prepared=null;
                Array.Clear(machine.Slots,0,machine.Slots.Length);
                geometry.SetPress(lane,0f); LastBlockedReason="";
                if (!session.IsPresentation) BatchCompleted?.Invoke(batch.BatchId);
                else session.MaximumObjectCount=Mathf.Max(session.MaximumObjectCount,session.ObjectRoot.GetComponentsInChildren<Transform>(true).Length);
            }
        }
        private bool CommitBatch(RecyclingSession session,FinishedBatch batch)
        {
            // This is a behavioural boundary, not merely a different ID prefix.
            // Presentations NEVER enter the runtime transaction or formal events.
            if (session.IsPresentation) return true;
            EcoAdventureRuntime runtime=EcoAdventureRuntime.Instance;
            if (!runtime || !runtime.TryCommitRecyclingBatch(batch.BatchId,batch.MaterialType,batch.SourceMaterialIds)) return false;
            session.ReceiptsCommitted++; return true;
        }
        private FinishedBatch PrepareProduct(int lane,MachineWork machine,RecyclingSession session)
        {
            string id=session.SessionId+"/"+lane+"/"+machine.Completed;
            FinishedBatch batch=new FinishedBatch {BatchId=id,MaterialType=machine.Slots[0].MaterialType,
                SourceMaterialIds=machine.Slots.Select(r=>r.MaterialId).ToArray(),
                Path=new TransportPath {ExitForward=Vector3.back}};
            foreach (C3ConveyorSection section in geometry.Machines[lane].OutputSections) batch.Path.Add(section);
            batch.OutputDistance=batch.Path.Legs.Take(2).Sum(s=>s.Length);
            Leg last=batch.Path.Legs[batch.Path.Legs.Count-1];
            // Fill the two outer slots before the centre. A final diagonal
            // delivery to the right would sweep through an occupied centre bale.
            float collectionOffset=machine.Completed==0 ? -.65f : machine.Completed==1 ? .65f : 0f;
            last.B+=Vector3.right*collectionOffset;
            // Validate the physical output before allocating scene objects. A
            // broken/missing surface must not leak a new hidden bale each retry.
            batch.Path.Evaluate(0f,0f,ProductSize,out Vector3 position,out Quaternion rotation,out _);
            GameObject item=new GameObject((session.IsPresentation?"P22_FINISHED_":"P2_FINISHED_")+machine.Slots[0].MaterialType+"_"+id);
            item.transform.SetParent(session.ObjectRoot,false); item.SetActive(false);
            try { visuals.CreateBale(batch.MaterialType,item.transform); }
            catch { item.SetActive(false); Destroy(item); throw; }
            batch.Product=item.transform;
            item.transform.SetPositionAndRotation(position,rotation);
            return batch;
        }
        private void TickProducts(RecyclingSession session,float dt)
        {
            foreach (FinishedBatch batch in session.Batches)
            {
                if (batch.Collected) continue;
                int segment;
                batch.Distance=batch.Path.Move(batch.Product,batch.Distance,
                    Mathf.Min(batch.Path.Length,batch.Distance+2f*dt),2f*dt,0f,ProductSize,out segment);
                batch.CurrentRouteSegment=segment;
                MaterialRecord[] source=session.Records.Where(r=>r.BatchId==batch.BatchId).ToArray();
                foreach (MaterialRecord record in source) record.CurrentRouteSegment=segment;
                if (!batch.OutputReached && batch.Distance>=batch.OutputDistance)
                {
                    batch.OutputReached=true;
                    foreach (MaterialRecord record in source) Transition(record,C3MaterialState.Outfeeding);
                }
                if (batch.Distance<batch.Path.Length-.0001f) continue;
                batch.Collected=true;
                foreach (MaterialRecord record in source) Transition(record,C3MaterialState.Collected);
                BoxCollider collider=batch.Product.gameObject.AddComponent<BoxCollider>(); collider.size=ProductSize;
            }
        }
        internal bool TryStartPresentation(C3PuzzleConsole console)
        {
            if (!CanStartPresentation || !console || !console.AuthorizesPresentation(this,device)) return false;
            string id="PRESENTATION-"+Guid.NewGuid().ToString("N");
            Transform root=new GameObject("P22_PRESENTATION_"+id).transform; root.SetParent(geometry.BuildRoot,false);
            presentation=new RecyclingSession(id,true,root);
            try
            {
                foreach (FinishedBatch batch in batches)
                {
                    GameObject product=batch.Product.gameObject;
                    formalVisibility.Add(new FormalVisibility {Object=product,Active=product.activeSelf});
                    // Inactive objects remove BOTH renderers and colliders from
                    // the world; their individual enabled states remain intact.
                    product.SetActive(false);
                }
                for (int i=0;i<9;i++)
                    CreateCargo(presentation,id+"/raw/"+i,i%3==0?EcoRecycleCargo.Kind.Plastic:
                        i%3==1?EcoRecycleCargo.Kind.Metal:EcoRecycleCargo.Kind.Paper);
                presentation.MaximumObjectCount=root.GetComponentsInChildren<Transform>(true).Length;
                return true;
            }
            catch (Exception error)
            {
                FinishPresentation(PresentationState.Failed,error.Message);
                Debug.LogException(error,this); return false;
            }
        }
        public void CancelPresentation()
        {
            if (presentation!=null) FinishPresentation(PresentationState.Cancelled,"");
        }
        private void FinishPresentation(PresentationState state,string failure)
        {
            RecyclingSession session=presentation;
            if (session==null) return;
            presentation=null;
            try
            {
                LastPresentation=new PresentationResult {
                    SessionId=session.SessionId,State=state,ReceiptsCommitted=session.ReceiptsCommitted,
                    MaximumObjectCount=session.MaximumObjectCount,ElapsedSeconds=session.ElapsedSeconds,Failure=failure,
                    Materials=session.Records.Select(r=>new PresentedMaterial {
                        MaterialId=r.MaterialId,MaterialType=r.MaterialType,TargetMachine=r.TargetMachine,
                        ObjectId=r.Cargo?r.Cargo.GetInstanceID():0,History=r.History.ToArray(),
                        ProcessingComplete=r.ProcessingComplete,Counted=r.Counted }).ToArray(),
                    Products=session.Batches.Select(b=>new PresentedProduct {
                        BatchId=b.BatchId,MaterialType=b.MaterialType,SourceMaterialIds=(string[])b.SourceMaterialIds.Clone(),
                        ObjectId=b.Product?b.Product.GetInstanceID():0,Position=b.Product?b.Product.position:Vector3.zero,
                        Rotation=b.Product?b.Product.rotation:Quaternion.identity,Collected=b.Collected,OutputReached=b.OutputReached }).ToArray()
                };
            }
            finally
            {
                try
                {
                    if (session.ObjectRoot) { session.ObjectRoot.gameObject.SetActive(false); Destroy(session.ObjectRoot.gameObject); }
                    session.ObjectRoot=null;
                    foreach (MachineWork machine in session.Work)
                    {
                        Array.Clear(machine.Slots,0,machine.Slots.Length); machine.Prepared=null;
                        machine.Processing=false; machine.Elapsed=0; machine.Completed=0;
                    }
                    session.Records.Clear(); session.Batches.Clear(); session.Accepted.Clear();
                }
                finally
                {
                    foreach (FormalVisibility saved in formalVisibility)
                        if (saved.Object) saved.Object.SetActive(saved.Active);
                    formalVisibility.Clear();
                    if (geometry && geometry.BuildRoot)
                        for (int lane=0;lane<geometry.Machines.Count;lane++)
                            if (geometry.Machines[lane].Press && geometry.Machines[lane].Pusher)
                            { geometry.SetPress(lane,0f); geometry.SetDiverter(lane,0f,false,1f); }
                }
            }
        }
        private void OnDestroy()
        {
            CancelPresentation();
            if (cargoRoot) { cargoRoot.gameObject.SetActive(false); Destroy(cargoRoot.gameObject); }
        }
    }
}
