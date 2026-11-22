using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SDGAdventure.Level03StoryV2
{
    // Live uGUI sorting game. Artwork is split into non-interactive decorative sprites;
    // every item, bin, meter, power decision and pointer hit test is a real runtime control.
    [DisallowMultipleComponent]
    public sealed class SDGLevel03R5SortingEnergyGame : MonoBehaviour
    {
        public const string GameVersion = "V2.6.2-R5.0";
        public const int TotalItems = 20;
        public const int ItemsPerWave = 4;
        public const int WaveCount = 5;
        public const int PowerLimit = 100;
        public const int RequiredPower = 90;
        private const float RoundDuration = 95f;
        private static readonly int[] Loads = {35,30,25,20};
        private static readonly string[] LoadNames = {"污染處理系統","安全通風系統","回收輸送帶","廠區照明"};
        private static readonly string[] CategoryNames = {"再生塑膠","再生金屬","電子廢棄物","一般垃圾"};
        private static readonly string[] BinArt = {"Plastic","Metal","Electronics","General"};
        private static readonly string[] ItemArt = {
            "Bottle","Can","PCB","Bag",
            "Tin","Detergent","Battery","Tissue",
            "Phone","Scrap","Cup","Wrapper",
            "PCB","Bottle","Tin","Bag",
            "Detergent","Can","Battery","Tissue"};
        private static readonly int[] ItemBins = {
            0,1,2,3, 1,0,2,3, 2,1,0,3, 2,0,1,3, 0,1,2,3};
        private static readonly string[] ItemNames = {
            "寶特瓶","鋁罐","廢電路板","垃圾袋",
            "鐵罐","塑膠容器","廢電池","髒污衛生紙",
            "舊手機","廢金屬","塑膠杯","複合包裝袋",
            "壞電路板","塑膠瓶","金屬容器","一般垃圾",
            "塑膠清潔瓶","飲料鋁罐","鈕扣電池","髒紙巾"};
        private static readonly Color Cyan = new Color(.18f,.94f,.95f,1f);
        private static readonly Color Green = new Color(.31f,1f,.57f,1f);
        private static readonly Color Amber = new Color(1f,.76f,.24f,1f);
        private static readonly Color Red = new Color(1f,.34f,.28f,1f);
        private static readonly Color Ink = new Color(.025f,.13f,.17f,.96f);
        private static readonly Color[] BinColors = {
            new Color(.19f,.70f,1f,1f),new Color(1f,.78f,.26f,1f),
            new Color(.28f,1f,.59f,1f),new Color(.72f,.60f,.94f,1f)
        };
        private enum Phase { Sorting, Ready, Won }
        private Phase phase;
        private RectTransform root;
        private Font font;
        private Action completed;
        private Text title,subtitle,waveLabel,timerLabel,energyLabel,scoreLabel,comboLabel,itemLabel;
        private Text[] binText=new Text[4],systemText=new Text[4],systemState=new Text[4];
        private Image[] binGlow=new Image[4],systemFill=new Image[4],systemBase=new Image[4];
        private RectTransform[] bins=new RectTransform[4];
        private bool[] loadSelected=new bool[4];
        private Image energyFill,progressFill,dragIcon,buttonBackground;
        private Text progressLabel,powerStatus,powerButtonLabel,instruction,sortingFeedback;
        private Button powerButton;
        private RectTransform dragRect;
        private SDGLevel03R5TrashDraggable draggable;
        private readonly List<Image> animatedChevrons=new List<Image>();
        private readonly List<RectTransform> powerBeads=new List<RectTransform>();
        private readonly List<Sprite> runtimeSprites=new List<Sprite>();
        private Image[] sparks=new Image[12];
        private Vector2[] sparkVelocities=new Vector2[12];
        private float sparkBorn=-10f;
        private Vector2 sparkOrigin;
        private bool dragging,finishedCallback;
        private float timeRemaining,animAt,nextClockRefresh;
        private int processed,correct,wrong,combo,bestCombo,power;
        private readonly int[] shuffledItems=new int[TotalItems];
        private Vector2 itemHome=new Vector2(-252f,76f);

        public void Setup(RectTransform parent,Action onCompleted)
        {
            completed=onCompleted;
            root=GetComponent<RectTransform>();
            root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);
            root.pivot=new Vector2(.5f,.5f);
            root.anchoredPosition=Vector2.zero;
            root.sizeDelta=new Vector2(1480f,834f);
            font=Font.CreateDynamicFontFromOSFont(
                new[]{"Microsoft JhengHei","Microsoft YaHei","Noto Sans CJK TC","Arial"},22);
            if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MakeArt("Industrial background (no fake items/indicators)",root,"SDG_R5_Panel",
                Vector2.zero,new Vector2(1480f,834f));
            title=MakeText("電網③　垃圾分類供能",root,new Vector2(0,355),new Vector2(930,70),47,Cyan);
            subtitle=MakeText("拖曳垃圾到正確分類箱，轉換電能並維持必要設備供電！",root,
                new Vector2(0,303),new Vector2(1090,36),20,Color.white);
            waveLabel=MakeText("第 1 / 5 波",root,new Vector2(-562,257),new Vector2(255,52),29,Color.white);
            timerLabel=MakeText("剩餘時間 01:35",root,new Vector2(571,256),new Vector2(250,54),27,Cyan);
            energyLabel=MakeText("可用電能 0 / 100",root,new Vector2(-12,257),new Vector2(450,51),29,Green);
            Image energyBase=Solid("Energy progress track",root,new Vector2(-10,219),new Vector2(483,16),new Color(.02f,.10f,.13f,.93f));
            energyFill=Solid("REAL energy produced progress",energyBase.rectTransform,new Vector2(-241.5f,0),new Vector2(483,15),Green);
            energyFill.rectTransform.pivot=new Vector2(0,.5f);
            energyFill.rectTransform.anchoredPosition=new Vector2(-241.5f,0);
            scoreLabel=MakeText("任務評價　☆☆☆",root,new Vector2(-566,212),new Vector2(270,38),22,Amber);
            comboLabel=MakeText("Combo ×0",root,new Vector2(-565,161),new Vector2(265,54),30,Amber);
            MakeArt("Conveyor is separate decorative sprite",root,"SDG_R5_Belt",
                new Vector2(-255,78),new Vector2(930,180));
            for(int i=0;i<14;i++)
            {
                Image chevron=Solid("Moving conveyor chevron "+i,root,
                    new Vector2(-662+i*59,18),new Vector2(25,5),new Color(.15f,.92f,.95f,.76f));
                chevron.rectTransform.localRotation=Quaternion.Euler(0,0,-22f);
                animatedChevrons.Add(chevron);
            }
            Image scanner=Solid("Scanner head",root,new Vector2(-255,170),new Vector2(210,30),
                new Color(.18f,.30f,.34f,1f));
            Solid("Scanner orange warning",scanner.rectTransform,new Vector2(0,0),new Vector2(92,15),Amber);
            Image scannerBeam=Solid("Scanner active cone",root,new Vector2(-255,96),new Vector2(130,110),
                new Color(.16f,1f,.52f,.10f));
            scannerBeam.raycastTarget=false;
            // Animated green pipes are independent of the decorative background.
            for(int i=0;i<4;i++)
            {
                float bx=-575f+i*214f;
                Solid("Bin power cable "+i,root,new Vector2(bx,-271),new Vector2(12,81),
                    new Color(.10f,.60f,.43f,.76f));
            }
            Solid("Energy bus",root,new Vector2(-245,-313),new Vector2(690,9),new Color(.08f,.81f,.52f,.80f));
            Solid("Energy vertical output",root,new Vector2(169,-151),new Vector2(9,319),new Color(.07f,.69f,.52f,.85f));
            for(int i=0;i<8;i++)
                powerBeads.Add(Solid("Moving real power bead "+i,root,
                    new Vector2(-570+i*91,-313),new Vector2(13,13),Green).rectTransform);
            // Four real bin hit regions: same RectTransforms are used by drag-drop hit tests.
            for(int i=0;i<4;i++)
            {
                float x=-575f+i*214f;
                Image bin=MakeArt("REAL target bin "+CategoryNames[i],root,
                    "SDG_R5_Bin_"+BinArt[i],new Vector2(x,-154),new Vector2(203,214));
                bins[i]=bin.rectTransform;
                binGlow[i]=Solid("Live hover highlight "+i,bin.rectTransform,
                    new Vector2(0,-3),new Vector2(155,102),new Color(BinColors[i].r,BinColors[i].g,BinColors[i].b,.03f));
                binText[i]=MakeText(CategoryNames[i],bin.rectTransform,new Vector2(0,-33),
                    new Vector2(171,45),22,Color.white);
                MakeText(new[]{"瓶、杯、容器","鋁罐、鐵罐","電池、電路板","髒污、不可回收"}[i],
                    bin.rectTransform,new Vector2(0,-68),new Vector2(179,40),15,BinColors[i]);
            }
            Image reactor=Solid("Real waste-to-energy converter",root,new Vector2(-248,-292),
                new Vector2(208,68),new Color(.035f,.28f,.26f,1f));
            Outline(reactor.rectTransform,205,65,Green);
            MakeText("♻ 垃圾轉換發電",reactor.rectTransform,Vector2.zero,new Vector2(204,63),24,Green);
            // Drag target is always a LIVE image, never a baked item.
            dragIcon=Solid("REAL draggable garbage",root,itemHome,new Vector2(132,132),Color.white);
            dragIcon.raycastTarget=true;
            dragRect=dragIcon.rectTransform;
            draggable=dragIcon.gameObject.AddComponent<SDGLevel03R5TrashDraggable>();
            draggable.Owner=this;
            itemLabel=MakeText("",root,new Vector2(-245,-2),new Vector2(230,31),23,Color.white);
            sortingFeedback=MakeText("",root,new Vector2(-245,-31),new Vector2(640,26),18,Green);
            // Right-side load rows; each one is a real switch with an independent state.
            MakeText("工廠電力分配狀態",root,new Vector2(483,178),new Vector2(426,48),30,Cyan);
            for(int i=0;i<4;i++)
            {
                int row=i;
                float y=106f-i*92f;
                systemBase[i]=Solid("Power system switch "+i,root,new Vector2(485,y),
                    new Vector2(418,80),Ink);
                Outline(systemBase[i].rectTransform,415,77,new Color(.09f,.76f,.73f,1f));
                Button b=systemBase[i].gameObject.AddComponent<Button>();
                systemBase[i].raycastTarget=true;
                b.onClick.AddListener(()=>ToggleLoad(row));
                systemText[i]=MakeText(LoadNames[i]+"　"+Loads[i]+"/100",systemBase[i].rectTransform,
                    new Vector2(-23,17),new Vector2(365,34),21,Color.white);
                Image meter=Solid("Consumption meter background "+i,systemBase[i].rectTransform,
                    new Vector2(-50,-20),new Vector2(234,12),new Color(.035f,.15f,.15f,1f));
                systemFill[i]=Solid("LIVE consumption meter "+i,meter.rectTransform,
                    new Vector2(-117,0),new Vector2(10,11),BinColors[2]);
                systemFill[i].rectTransform.pivot=new Vector2(0,.5f);
                systemState[i]=MakeText("未供電",systemBase[i].rectTransform,
                    new Vector2(150,-19),new Vector2(112,31),18,Amber);
            }
            Image hint=Solid("Energy education hint",root,new Vector2(487,-274),
                new Vector2(415,92),new Color(.19f,.14f,.025f,.90f));
            Outline(hint.rectTransform,412,89,Amber);
            powerStatus=MakeText("必要設備 90 單位，全部啟用則 110 單位會過載。",hint.rectTransform,
                Vector2.zero,new Vector2(394,77),19,Amber);
            Image instr=Solid("Sorting instructions",root,new Vector2(-573,-367),
                new Vector2(291,70),new Color(.015f,.18f,.21f,.90f));
            Outline(instr.rectTransform,288,67,Cyan);
            instruction=MakeText("按住左鍵拖曳物品 → 對應分類箱",instr.rectTransform,
                Vector2.zero,new Vector2(275,64),18,Color.white);
            Image pbase=Solid("Wave progress background",root,new Vector2(-158,-365),
                new Vector2(500,67),new Color(.02f,.16f,.19f,.94f));
            Outline(pbase.rectTransform,497,64,Cyan);
            progressLabel=MakeText("本波分類進度 0/20",pbase.rectTransform,
                new Vector2(0,15),new Vector2(474,33),21,Color.white);
            Image track=Solid("Wave progress track",pbase.rectTransform,
                new Vector2(0,-18),new Vector2(442,11),new Color(.015f,.11f,.14f,1f));
            progressFill=Solid("REAL wave progress",track.rectTransform,
                new Vector2(-221,0),new Vector2(0,10),Green);
            progressFill.rectTransform.pivot=new Vector2(0,.5f);
            Image gold=Solid("LIVE power button",root,new Vector2(490,-370),
                new Vector2(370,83),new Color(.75f,.42f,.045f,1f));
            buttonBackground=gold;
            Outline(gold.rectTransform,364,78,new Color(1f,.90f,.43f,1f));
            powerButton=gold.gameObject.AddComponent<Button>();
            gold.raycastTarget=true;
            powerButton.onClick.AddListener(TrySupply);
            powerButtonLabel=MakeText("啟動供電",gold.rectTransform,Vector2.zero,
                new Vector2(350,72),36,new Color(1f,.98f,.78f,1f));
            // Actual short-lived particles; nothing is permanently painted in a picture.
            for(int i=0;i<sparks.Length;i++)
            {
                sparks[i]=Solid("Live sorting electric sparkle "+i,root,
                    new Vector2(-200,-130),new Vector2(12,12),Green);
                sparks[i].gameObject.SetActive(false);
            }
            gameObject.SetActive(false);
        }

        public void Begin()
        {
            gameObject.SetActive(true);
            phase=Phase.Sorting;
            finishedCallback=false;
            timeRemaining=RoundDuration;
            processed=0;correct=0;wrong=0;combo=0;bestCombo=0;power=0;dragging=false;
            for(int i=0;i<4;i++)loadSelected[i]=i<3;
            // Keep one of each waste category per wave; randomize within each wave.
            for(int i=0;i<TotalItems;i++)shuffledItems[i]=i;
            for(int w=0;w<WaveCount;w++)
                for(int i=ItemsPerWave-1;i>0;i--)
                {
                    int a=w*ItemsPerWave+i;
                    int b=w*ItemsPerWave+UnityEngine.Random.Range(0,i+1);
                    int tmp=shuffledItems[a];shuffledItems[a]=shuffledItems[b];shuffledItems[b]=tmp;
                }
            sortingFeedback.text="將輸送帶上的垃圾拖入對應分類箱";
            sortingFeedback.color=Cyan;
            powerButtonLabel.text="啟動供電";
            powerButton.interactable=true;
            buttonBackground.color=new Color(.75f,.42f,.045f,1f);
            SpawnItem();
            RefreshUI();
        }
        public void Hide(){gameObject.SetActive(false);}
        private void SpawnItem()
        {
            if(processed>=TotalItems)
            {
                phase=Phase.Ready;
                dragIcon.gameObject.SetActive(false);
                itemLabel.text="分類完成";
                sortingFeedback.text=power>=RequiredPower?"已取得足夠電能，可啟動必要設備供電。":"電能不足 90，請點擊重新挑戰。";
                sortingFeedback.color=power>=RequiredPower?Green:Amber;
                powerButtonLabel.text=power>=RequiredPower?"啟動供電":"重新挑戰";
                return;
            }
            string art="SDG_R5_Item_"+ItemArt[shuffledItems[processed]];
            Sprite sprite=LoadSprite(art);
            dragIcon.sprite=sprite;
            dragIcon.preserveAspect=true;
            dragIcon.raycastTarget=true;
            dragIcon.color=Color.white;
            dragIcon.rectTransform.anchoredPosition=itemHome;
            dragIcon.gameObject.SetActive(true);
            itemLabel.text=ItemNames[shuffledItems[processed]];
        }
        public void BeginDrag()
        {
            if(phase!=Phase.Sorting || processed>=TotalItems)return;
            dragging=true;
            dragRect.SetAsLastSibling();
            // Keep the feedback and button above the dragged object.
            powerButton.transform.SetAsLastSibling();
        }
        public void Drag(Vector2 screenPosition)
        {
            if(!dragging || phase!=Phase.Sorting)return;
            Vector2 p;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screenPosition,null,out p))
                dragRect.anchoredPosition=new Vector2(Mathf.Clamp(p.x,-660,660),Mathf.Clamp(p.y,-350,185));
            for(int i=0;i<4;i++)
            {
                bool hover=RectTransformUtility.RectangleContainsScreenPoint(bins[i],screenPosition,null);
                Color c=BinColors[i]; c.a=hover?.40f:.035f; binGlow[i].color=c;
            }
        }
        public void EndDrag(Vector2 screenPosition)
        {
            if(!dragging || phase!=Phase.Sorting)return;
            dragging=false;
            int found=-1;
            for(int i=0;i<4;i++)
            {
                if(RectTransformUtility.RectangleContainsScreenPoint(bins[i],screenPosition,null))found=i;
                Color c=BinColors[i];c.a=.035f;binGlow[i].color=c;
            }
            if(found<0)
            {
                dragRect.anchoredPosition=itemHome;
                sortingFeedback.text="請把物品拖入分類箱內";
                sortingFeedback.color=Cyan;
                return;
            }
            bool right=found==ItemBins[shuffledItems[processed]];
            if(right)
            {
                correct++;combo++;bestCombo=Mathf.Max(bestCombo,combo);
                int extra=combo>=6?4:combo>=3?2:0;
                power=Mathf.Min(PowerLimit,power+6+extra);
                sortingFeedback.text="分類正確！+"+(6+extra)+" 電能"+(combo>=3?"　Combo ×"+combo:"");
                sortingFeedback.color=Green;
            }
            else
            {
                wrong++;combo=0;power=Mathf.Max(0,power-5);
                sortingFeedback.text="分類錯誤！「"+ItemNames[shuffledItems[processed]]+"」應放入「"+CategoryNames[ItemBins[shuffledItems[processed]]]+"」";
                sortingFeedback.color=Red;
            }
            SparkAt(bins[found].anchoredPosition,right);
            processed++;
            SpawnItem();
            RefreshUI();
        }
        private void ToggleLoad(int row)
        {
            if(phase==Phase.Won)return;
            loadSelected[row]=!loadSelected[row];
            RefreshUI();
        }
        private void TrySupply()
        {
            if(phase==Phase.Sorting)
            {
                sortingFeedback.text="請先完成五波、共 20 件物品的分類！";
                sortingFeedback.color=Amber;
                return;
            }
            if(phase==Phase.Won)return;
            // Completing all five waves is mandatory, not just accumulating energy.
            if(processed<TotalItems || power<RequiredPower)
            {
                Begin();
                return;
            }
            int used=0;
            for(int i=0;i<4;i++)if(loadSelected[i])used+=Loads[i];
            if(!loadSelected[0]||!loadSelected[1]||!loadSelected[2])
            {
                powerStatus.text="污染處理、安全通風和輸送帶必須全部供電。";
                powerStatus.color=Red;return;
            }
            if(used>PowerLimit || used>power)
            {
                powerStatus.text="電網過載！需求 "+used+" / 可用 "+power+"，請關閉非必要照明。";
                powerStatus.color=Red;return;
            }
            phase=Phase.Won;
            sortingFeedback.text="分類供能成功！電網③ 已恢復。";
            sortingFeedback.color=Green;
            powerStatus.text="供電成功："+used+" / "+power+"，必要系統正常。";
            powerStatus.color=Green;
            powerButtonLabel.text="供電完成";
            RefreshUI();
            if(!finishedCallback && completed!=null)
            {
                finishedCallback=true;
                completed();
            }
        }
        private int UsedPower()
        {
            int n=0;for(int i=0;i<4;i++)if(loadSelected[i])n+=Loads[i];return n;
        }
        private void RefreshUI()
        {
            waveLabel.text="第 "+Mathf.Min(WaveCount,processed/ItemsPerWave+1)+" / "+WaveCount+" 波";
            energyLabel.text="可用電能 "+power+" / "+PowerLimit;
            energyFill.rectTransform.sizeDelta=new Vector2(483f*power/PowerLimit,15);
            int stars=correct>=18?3:correct>=14?2:correct>=8?1:0;
            scoreLabel.text="任務評價　"+new string('★',stars)+new string('☆',3-stars);
            comboLabel.text=combo>=3?"Combo ×"+combo+"　連續正確！":"正確 "+correct+"　失誤 "+wrong;
            progressLabel.text="本波分類進度 "+processed+" / "+TotalItems;
            progressFill.rectTransform.sizeDelta=new Vector2(442f*processed/TotalItems,10);
            int demand=UsedPower();
            for(int i=0;i<4;i++)
            {
                bool selected=loadSelected[i];
                systemFill[i].rectTransform.sizeDelta=new Vector2(234f*(selected?Loads[i]:0)/PowerLimit,11);
                systemText[i].text=(selected?"✓ ":"○ ")+LoadNames[i]+"　"+Loads[i]+"/100";
                bool overload=selected && demand>power;
                systemState[i].text=!selected?"關閉":overload?"待供電":phase==Phase.Won?"穩定":"已選定";
                systemState[i].color=overload?Amber:selected?Green:Color.gray;
                systemBase[i].color=selected?new Color(.04f,.27f,.23f,.93f):Ink;
            }
            if(phase!=Phase.Won)
            {
                powerStatus.text="供電需求 "+demand+" / 可用 "+power+
                    (demand>PowerLimit?"　過載！":demand>power?"　電能不足":"　負載正常");
                powerStatus.color=demand>PowerLimit?Red:demand>power?Amber:Green;
            }
        }
        private void Update()
        {
            if(phase==Phase.Won)return;
            float dt=Time.unscaledDeltaTime;
            animAt+=dt;
            for(int i=0;i<animatedChevrons.Count;i++)
            {
                float x=-683f+Mathf.Repeat(i*59f+animAt*108f,849f);
                animatedChevrons[i].rectTransform.anchoredPosition=new Vector2(x,18);
            }
            for(int i=0;i<powerBeads.Count;i++)
                powerBeads[i].anchoredPosition=new Vector2(-593f+Mathf.Repeat(i*94f+animAt*96f,700f),-313);
            if(!dragging && phase==Phase.Sorting && dragRect.gameObject.activeSelf)
                dragRect.anchoredPosition=itemHome+new Vector2(Mathf.Sin(animAt*1.6f)*5f,Mathf.Sin(animAt*3.1f)*4f);
            if(phase==Phase.Sorting)
            {
                timeRemaining=Mathf.Max(0f,timeRemaining-dt);
                if(timeRemaining<=0f)
                {
                    phase=Phase.Ready;
                    dragIcon.gameObject.SetActive(false);
                    sortingFeedback.text="時間到！五波分類尚未完成，請重新挑戰。";
                    powerButtonLabel.text="重新挑戰";
                    RefreshUI();
                }
            }
            if(Time.unscaledTime>=nextClockRefresh)
            {
                nextClockRefresh=Time.unscaledTime+.12f;
                int seconds=Mathf.CeilToInt(timeRemaining);
                timerLabel.text="剩餘時間 "+(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
                timerLabel.color=seconds<=20?Red:Cyan;
            }
            if(animAt-sparkBorn<.50f)
            {
                float k=(animAt-sparkBorn)/.5f;
                for(int i=0;i<sparks.Length;i++)
                {
                    if(!sparks[i].gameObject.activeSelf)continue;
                    sparks[i].rectTransform.anchoredPosition=sparkOrigin+sparkVelocities[i]*(k*100f);
                    Color c=sparks[i].color;c.a=1f-k;sparks[i].color=c;
                }
            }
            else for(int i=0;i<sparks.Length;i++)if(sparks[i].gameObject.activeSelf)sparks[i].gameObject.SetActive(false);
        }
        private void SparkAt(Vector2 at,bool success)
        {
            sparkBorn=animAt;sparkOrigin=at;
            for(int i=0;i<sparks.Length;i++)
            {
                float theta=i*Mathf.PI*2f/sparks.Length;
                sparkVelocities[i]=new Vector2(Mathf.Cos(theta),Mathf.Sin(theta));
                sparks[i].color=success?Green:Red;
                sparks[i].transform.SetAsLastSibling();
                sparks[i].gameObject.SetActive(true);
            }
        }
        private static Image Solid(string name,Transform parent,Vector2 pos,Vector2 size,Color color)
        {
            GameObject g=new GameObject(name,typeof(RectTransform),typeof(Image));
            g.transform.SetParent(parent,false);
            RectTransform rect=g.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=pos;rect.sizeDelta=size;
            Image image=g.GetComponent<Image>();image.color=color;image.raycastTarget=false;
            return image;
        }
        private Image MakeArt(string name,Transform parent,string resource,Vector2 pos,Vector2 size)
        {
            Image image=Solid(name,parent,pos,size,Color.white);
            image.sprite=LoadSprite(resource);
            image.preserveAspect=true;
            return image;
        }
        private Sprite LoadSprite(string resource)
        {
            Texture2D tex=Resources.Load<Texture2D>(resource);
            if(!tex)
            {
                Debug.LogError("[SDG R5] ART MISSING: Resources/"+resource);
                return null;
            }
            Sprite result=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),
                new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
            runtimeSprites.Add(result);
            return result;
        }
        private Text MakeText(string name,Transform parent,Vector2 pos,Vector2 size,int px,Color color)
        {
            GameObject g=new GameObject(name,typeof(RectTransform),typeof(Text));
            g.transform.SetParent(parent,false);
            RectTransform rect=g.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=pos;rect.sizeDelta=size;
            Text label=g.GetComponent<Text>();label.font=font;label.fontSize=px;
            label.text=name;label.color=color;label.alignment=TextAnchor.MiddleCenter;
            label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;
            label.verticalOverflow=VerticalWrapMode.Overflow;
            return label;
        }
        private static void Outline(Transform parent,float w,float h,Color color)
        {
            Solid("Top neon rim",parent,new Vector2(0,h*.5f),new Vector2(w,3),color);
            Solid("Bottom neon rim",parent,new Vector2(0,-h*.5f),new Vector2(w,3),color);
            Solid("Left neon rim",parent,new Vector2(-w*.5f,0),new Vector2(3,h),color);
            Solid("Right neon rim",parent,new Vector2(w*.5f,0),new Vector2(3,h),color);
        }
        private void OnDestroy()
        {
            foreach(Sprite sprite in runtimeSprites)if(sprite)Destroy(sprite);
        }
    }

    // Pointer events use the visible sprite's RectTransform and screen positions.
    // The actual category is NOT inferred from the pictured art; it is the data table above.
    public sealed class SDGLevel03R5TrashDraggable : MonoBehaviour,
        IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public SDGLevel03R5SortingEnergyGame Owner;
        public void OnBeginDrag(PointerEventData eventData){if(Owner)Owner.BeginDrag();}
        public void OnDrag(PointerEventData eventData){if(Owner)Owner.Drag(eventData.position);}
        public void OnEndDrag(PointerEventData eventData){if(Owner)Owner.EndDrag(eventData.position);}
    }
}
