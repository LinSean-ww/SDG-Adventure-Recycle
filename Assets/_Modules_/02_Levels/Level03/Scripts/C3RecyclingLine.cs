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
        private sealed class MachineWork
        {
            public readonly MaterialRecord[] Slots=new MaterialRecord[UnitsPerBale];
            public bool Processing;
            public float Elapsed;
            public int Completed;
            public FinishedBatch Prepared;
        }
        private C3DeviceState device;
        private C3RecyclingLineGeometry geometry;
        private Transform cargoRoot;
        private readonly List<MaterialRecord> records=new List<MaterialRecord>();
        private readonly List<FinishedBatch> batches=new List<FinishedBatch>();
        private readonly HashSet<string> accepted=new HashSet<string>(StringComparer.Ordinal);
        private readonly MachineWork[] work={new MachineWork(),new MachineWork(),new MachineWork()};
        private bool supplied;
        private float releaseClock;
        private string runId;
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
            runId=Guid.NewGuid().ToString("N");
            cargoRoot=new GameObject("P2_TRACKED_MATERIALS").transform;
            cargoRoot.SetParent(geometry.BuildRoot,false);
        }
        private bool CanAccept(string id,EcoRecycleCargo.Kind kind)
        {
            EcoAdventureRuntime runtime=EcoAdventureRuntime.Instance;
            int machine=MachineIndex(kind);
            return gameObject.activeInHierarchy && device.FactoryState!=C3FactoryState.Completed &&
                runtime && machine>=0 && !string.IsNullOrWhiteSpace(id) && id.Length<=128 &&
                !accepted.Contains(id) && !runtime.HasRecyclingReceipt(id) &&
                records.Count(r=>r.TargetMachine==machine)<UnitsPerBale*BalesPerMachineCapacity &&
                runtime.RecyclingReceiptCount+records.Count(r=>!r.Counted)<EcoAdventureRuntime.RecyclingReceiptCapacity;
        }
        public bool TrySupply(string id,EcoRecycleCargo.Kind kind)
        {
            if (!CanAccept(id,kind)) return false;
            GameObject item=new GameObject("P2_RAW_"+id);
            item.transform.SetParent(cargoRoot,false);
            item.transform.position=geometry.BMaterialInterface.position+Vector3.up*C3RecyclingLineGeometry.RawRadius;
            GameObject model=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            model.name="RAW_MATERIAL"; model.transform.SetParent(item.transform,false);
            model.transform.localScale=Vector3.one*(C3RecyclingLineGeometry.RawRadius*2f);
            model.GetComponent<Renderer>().sharedMaterial=geometry.MaterialFor(MachineIndex(kind));
            Collider collider=model.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
            EcoRecycleCargo cargo=item.AddComponent<EcoRecycleCargo>();
            cargo.materialId=id; cargo.materialType=kind;
            Admit(cargo); return true;
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
            if (renderers.Length==0 || renderers.Any(r=>r.bounds.size.magnitude>.43f)) return false;
            Admit(cargo); return true;
        }
        private void Admit(EcoRecycleCargo cargo)
        {
            accepted.Add(cargo.materialId); cargo.lineOwned=true;
            cargo.transform.SetParent(cargoRoot,true);
            MaterialRecord record=new MaterialRecord {MaterialId=cargo.materialId,MaterialType=cargo.materialType,
                TargetMachine=MachineIndex(cargo.materialType),Cargo=cargo,State=C3MaterialState.Spawned};
            record.Visual=cargo.GetComponentInChildren<Renderer>(true).transform;
            record.VisualScale=record.Visual.localScale;
            record.History.Add(record.State);
            record.Path=new TransportPath {ExitNormal=geometry.Machines[record.TargetMachine].Chute.Normal};
            foreach (C3ConveyorSection section in geometry.CommonSections) record.Path.Add(section);
            record.Path.Legs.Add(new Leg {A=geometry.MainBelt.Start.position,
                B=geometry.Machines[record.TargetMachine].Chute.Start.position,Support=geometry.MainBelt.Surface});
            record.Cargo.gameObject.SetActive(false);
            records.Add(record);
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
            releaseClock+=dt;
            MaterialRecord waiting=records.FirstOrDefault(r=>r.State==C3MaterialState.Spawned);
            if (waiting!=null && releaseClock>=.45f && records.Where(OnPrimary).All(r=>r.Distance>MinimumSpacing))
            {
                releaseClock=0f; Transition(waiting,C3MaterialState.Feeding);
                waiting.Cargo.gameObject.SetActive(true); PlaceRaw(waiting);
            }
            MaterialRecord[] alreadyDiverting=records.Where(r=>r.State==C3MaterialState.Diverting).ToArray();
            foreach (MaterialRecord record in records.Where(OnPrimary).OrderByDescending(r=>r.Distance).ToArray())
            {
                if (record.State==C3MaterialState.Identified) { TryDivert(record); continue; }
                float next=Mathf.Min(record.Path.Length,record.Distance+TransportSpeed*dt);
                foreach (MaterialRecord ahead in records.Where(OnPrimary))
                    if (ahead!=record && ahead.Distance>record.Distance+.001f) next=Mathf.Min(next,ahead.Distance-MinimumSpacing);
                MoveRaw(record,Mathf.Max(record.Distance,next),TransportSpeed*dt);
                float commonLength=record.Path.Legs.Take(geometry.CommonSections.Count).Sum(s=>s.Length);
                if (record.State==C3MaterialState.Feeding && record.Distance>=commonLength) Transition(record,C3MaterialState.MainBelt);
                if (record.Distance>=record.Path.Length-.0001f)
                {
                    if (record.State==C3MaterialState.Feeding) Transition(record,C3MaterialState.MainBelt);
                    Transition(record,C3MaterialState.Identified); TryDivert(record);
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
            TickProducts(dt);
            TickMachines(dt);
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
        private void TryDivert(MaterialRecord record)
        {
            MachineWork machine=work[record.TargetMachine];
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
        private void TickMachines(float dt)
        {
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
                if (machine.Prepared==null) machine.Prepared=PrepareProduct(lane,machine);
                EcoAdventureRuntime runtime=EcoAdventureRuntime.Instance;
                FinishedBatch batch=machine.Prepared;
                if (!runtime || !runtime.TryCommitRecyclingBatch(batch.BatchId,batch.MaterialType,batch.SourceMaterialIds))
                {
                    LastBlockedReason="Processing receipt rejected; no output or duplicate credit emitted: "+batch.BatchId;
                    continue;
                }
                foreach (MaterialRecord record in machine.Slots)
                {
                    record.Counted=record.ProcessingComplete=record.ProductCreated=true;
                    record.BatchId=batch.BatchId; record.Cargo.processed=true;
                    record.CurrentRouteSegment=0;
                    Transition(record,C3MaterialState.OutputReady);
                    record.Cargo.gameObject.SetActive(false);
                }
                batch.Product.gameObject.SetActive(true); batches.Add(batch);
                lastCompleted=batch.MaterialType; machine.Completed++;
                machine.Processing=false; machine.Elapsed=0f; machine.Prepared=null;
                Array.Clear(machine.Slots,0,machine.Slots.Length);
                geometry.SetPress(lane,0f); LastBlockedReason="";
                BatchCompleted?.Invoke(batch.BatchId);
            }
        }
        private FinishedBatch PrepareProduct(int lane,MachineWork machine)
        {
            string id=runId+"/"+lane+"/"+machine.Completed;
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
            GameObject item=new GameObject("P2_FINISHED_"+machine.Slots[0].MaterialType+"_"+id);
            item.transform.SetParent(cargoRoot,false); item.SetActive(false);
            GameObject body=GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name=lane==0?"PAPER_COMPRESSED_BALE":lane==1?"METAL_COMPRESSED_BLOCK":"PLASTIC_COMPRESSED_BALE";
            body.transform.SetParent(item.transform,false); body.transform.localScale=new Vector3(.52f,.42f,.50f);
            body.GetComponent<Renderer>().sharedMaterial=geometry.MaterialFor(lane);
            Collider c=body.GetComponent<Collider>(); c.enabled=false; Destroy(c);
            int straps=lane==1?1:2;
            for (int i=0;i<straps;i++)
            {
                GameObject band=GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name=lane==1?"METAL_PRESS_SEAM":"BALE_BINDING";
                band.transform.SetParent(item.transform,false);
                band.transform.localPosition=new Vector3(straps==1?0f:(i==0?-.16f:.16f),0f,0f);
                band.transform.localScale=new Vector3(.025f,.43f,.51f);
                band.GetComponent<Renderer>().sharedMaterial=geometry.BindingMaterial;
                Collider collider=band.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
            }
            batch.Product=item.transform;
            item.transform.SetPositionAndRotation(position,rotation);
            return batch;
        }
        private void TickProducts(float dt)
        {
            foreach (FinishedBatch batch in batches)
            {
                if (batch.Collected) continue;
                int segment;
                batch.Distance=batch.Path.Move(batch.Product,batch.Distance,
                    Mathf.Min(batch.Path.Length,batch.Distance+2f*dt),2f*dt,0f,ProductSize,out segment);
                batch.CurrentRouteSegment=segment;
                MaterialRecord[] source=records.Where(r=>r.BatchId==batch.BatchId).ToArray();
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
        private void OnDestroy()
        {
            if (cargoRoot) { cargoRoot.gameObject.SetActive(false); Destroy(cargoRoot.gameObject); }
        }
    }
}
