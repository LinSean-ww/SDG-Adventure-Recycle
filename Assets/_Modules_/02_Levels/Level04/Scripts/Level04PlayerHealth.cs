using System;
using SDGAdventure.SharedPlayer;
using UnityEngine;
namespace SDGAdventure.Level04 {
 [DisallowMultipleComponent]
 [RequireComponent(typeof(SharedFirstPersonController))]
 public sealed class Level04PlayerHealth : MonoBehaviour {
  [SerializeField,Min(1)] private int maxHealth=100;
  [SerializeField,Min(0)] private float invulnerabilitySeconds=1.2f;
  [SerializeField,Min(0)] private float horizontalLaunchSpeed=7f;
  [SerializeField,Min(0)] private float upwardLaunchSpeed=6f;
  [SerializeField,Min(0)] private float controlLockSeconds=.25f;
  private int currentHealth;private float invulnerableUntil;private bool dead;private SharedFirstPersonController motor;
  public int CurrentHealth=>currentHealth;public int MaxHealth=>maxHealth;public bool IsDead=>dead;
  public event Action Changed;public event Action Damaged;public event Action Died;
  void Awake(){maxHealth=Mathf.Max(1,maxHealth);currentHealth=maxHealth;motor=GetComponent<SharedFirstPersonController>();}
  // Used only by the save-slot restorer after the scene's Awake/Start initialization.
  // Never modify the configured maximum or trigger damage/knockback while loading.
  public void RestoreFromSave(int savedHealth){
   int next=Mathf.Clamp(savedHealth,0,Mathf.Max(1,maxHealth));
   currentHealth=next;
   dead=next==0;
   invulnerableUntil=0f;
   if(dead&&motor)motor.ClearExternalImpulse();
   Changed?.Invoke();
   if(dead)Died?.Invoke();
  }
  public bool TryBossHit(BossHealth boss,int damage,Vector3 source){
   var input=PlayerInputService.Instance;
   if(!boss||boss.IsDead||dead||damage<=0||Time.timeScale<=0||Time.time<invulnerableUntil||!input||input.Context!=PlayerInputContext.Gameplay||input.GameplayActionsBlocked)return false;
   currentHealth=Mathf.Clamp(currentHealth-damage,0,maxHealth);invulnerableUntil=Time.time+Mathf.Max(0,invulnerabilitySeconds);
   if(currentHealth==0){dead=true;if(motor)motor.ClearExternalImpulse();}else if(motor){Vector3 direction=Vector3.ProjectOnPlane(transform.position-source,Vector3.up);if(direction.sqrMagnitude<.0001f)direction=-transform.forward;direction.Normalize();motor.ApplyExternalImpulse(direction*horizontalLaunchSpeed+Vector3.up*upwardLaunchSpeed,controlLockSeconds);}
   Changed?.Invoke();Damaged?.Invoke();if(dead)Died?.Invoke();return true;
  }
 }
}