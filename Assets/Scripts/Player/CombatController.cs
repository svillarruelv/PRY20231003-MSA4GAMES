using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CombatController : MonoBehaviour, IStatsDataProvider
{
  private AudioSource audioSource;
  public AudioClip hurtSound;
  public StatsData playerStats = new StatsData();

  private MovementController movementController;

  [NonSerialized]
  public GameObject enemy = null;

  [NonSerialized]
  public GameObject weaponInHands = null;

  [NonSerialized]
  public float range;
  [NonSerialized]
  public int damage;
  [NonSerialized]
  public int defaultDamage = 2;
  [NonSerialized]
  public float defaultRange = 1.5f;

  public Slider healthBar;
  public TMPro.TextMeshProUGUI uiText;
  public bool isTraining = false;

  public StatsData GetStatsData()
  {
    return playerStats;
  }

  public Vector3 GetPosition()
  {
    return transform.position;
  }

  public float GetMainMetric()
  {
    return (float)playerStats.points;
  }

  void Start()
  {
    damage = defaultDamage;
    range = defaultDamage;

    audioSource = GetComponent<AudioSource>();
    if (audioSource == null)
    {
      audioSource = gameObject.AddComponent<AudioSource>();
    }

    healthBar.maxValue = playerStats.health;
    healthBar.value = playerStats.health;

    playerStats.id = 0;

    JointQData data = SaveSystem.LoadQTable();
    if (data != null)
    {
      playerStats.points = data.score;
      UpdateScoreText();
    }

    movementController = GetComponent<MovementController>();
  }

  void Update()
  {
    if (Input.GetMouseButtonDown(0) && GetComponent<MovementController>().canMove)
    {
      Attack();
    }
    if (Input.GetMouseButton(1))
    {
      Block();
    }
    else if (Input.GetMouseButtonUp(1))
    {
      movementController.canMove = true;
      movementController.characterAnimator.SetBool("isBlocking", false);
    }
  }

  private void Attack()
  {
    movementController.canMove = false;
    movementController.characterAnimator.SetBool("isAttacking", true);

    if (weaponInHands)
    {
      range = weaponInHands.GetComponent<WeaponController>().weaponData.range;
      damage = weaponInHands.GetComponent<WeaponController>().weaponData.damage;
    }

    if (weaponInHands && weaponInHands.GetComponent<ShootingSystem>())
    {
      weaponInHands.GetComponent<ShootingSystem>().Shoot();
    }
    else if (enemy)
    {
      if (Vector3.Distance(transform.position, enemy.transform.position) <= range)
      {
        StartCoroutine(Utility.TimedEvent(() =>
        {
          if (enemy)
          {
            enemy.GetComponent<EnemyController>().TakeDamage(damage);
          }
        }, 1f));
      }
    }

    StartCoroutine(Utility.TimedEvent(() =>
    {
      GetComponent<MovementController>().canMove = true;
    }, 1.5f));
  }

  private void Block()
  {
    movementController.canMove = false;
    movementController.characterAnimator.SetBool("isBlocking", true);
  }

  public void TakeDamage(int damage, EnemyController enemy)
  {
    if (!movementController.characterAnimator.GetBool("isBlocking"))
    {
      healthBar.value -= damage;
      playerStats.health -= damage;
      movementController.characterAnimator.SetBool("isHit", true);
      audioSource.PlayOneShot(hurtSound);
#if UNITY_EDITOR
      //Record that the character was hurt
      FileManager.Instance.WriteAction(FileManager.ActionType.HURT,
                                            FileManager.ActionResult.SUCCESS,
                                            FileManager.CharacterType.PLAYER,
                                            this.GetComponent<IStatsDataProvider>(),
                                            enemy.GetComponent<IStatsDataProvider>());
#endif
      if (playerStats.health <= 0)
      {
        SaveSystem.SaveQTable(playerStats.points);
        if (!isTraining)
        {
#if UNITY_EDITOR

        //Record that the player was killed
        FileManager.Instance.WriteAction(FileManager.ActionType.HURT,
                                            FileManager.ActionResult.DEAD,
                                            FileManager.CharacterType.PLAYER,
                                            this.GetComponent<IStatsDataProvider>(),
                                            enemy.GetComponent<IStatsDataProvider>());
#endif
          movementController.characterAnimator.SetBool("isDead", true);
          CanvasManager.instance.Wasted();
        }
        else
        {
          playerStats.health = 0;
          healthBar.value = 0;
        }

      }
    }
  }

  public void UpdateScoreText()
  {
    uiText.text = $"Score: {playerStats.points}";
  }
}
