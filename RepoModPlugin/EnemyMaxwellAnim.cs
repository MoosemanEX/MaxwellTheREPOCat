using MaxwellTheCat;
using Photon.Pun;
using UnityEngine;

public class EnemyMaxwellAnim : MonoBehaviour
{
	public Enemy enemy;

	internal Animator animator;

	public EnemyMaxwell controller;
	
	public AudioSource music;

	public AudioSource riff;

	private float idleBreakerTimer;
	
	private ParticleScriptExplosion particleScriptExplosion;
    
	[Space] public Sound soundExplosion;

	public Sound soundExplosionGlobal;

	private void Awake()
	{
		animator = GetComponentInChildren<Animator>();
		animator.keepAnimatorStateOnDisable = true;
		music.Play();
	}

	private void Update()
	{
		if (enemy.Rigidbody.frozen)
		{
			animator.speed = 0f;
		}
		else
		{
			animator.speed = 1;
		}

		if (!enemy.IsStunned())
		{
			if (controller.currentState == EnemyMaxwell.State.AttackStart)
			{
				animator.SetBool("bEffectTriggered", true);
			}
		}
	}
	
	private void BttbSound()
	{
		riff.Play();
	}
	
	public void OnSpawn()
	{
		animator.Play("MaxwellWobble", 0, 0);
		music.Play();
	}

	public void Despawn()
	{
		enemy.EnemyParent.Despawn();
	}

	internal void ChangeToSkeleton()
	{
		animator.SetBool("bEffectTriggered", true);
		music.Stop();
	}

	void TriggerExplosion()
	{
		controller.SetStateExplode();
		animator.SetBool("bEffectTriggered", false);
	}
}
