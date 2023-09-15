using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemySpawner : MonoBehaviour
{
  public GameObject enemyPrefab;
  public GameObject playerObject;
  public PotionSpawner potionSpawner;

  public Transform[] spawnPoints;
  public int maxEnemiesPerSpawnPoint = 1;
  private bool areEnemiesDefeated = false;
  private bool areSpawning = false;
  public int hordeNumber = 1;
  public float mean_death_time = 0f;
  private int total_enemies;

  private void Start()
  {
    Debug.Log("Enemies spawned");
    SpawnInitialEnemies();
  }

  private void Update()
  {
    this.mean_death_time += Time.deltaTime;

    if (areEnemiesDefeated && !areSpawning)
    {
      areSpawning = true;
      potionSpawner.SpawnPotions();
      StartCoroutine(SpawnNewEnemiesCoroutine());
      Debug.Log($"New horde #{hordeNumber}");
    }
    CheckIfEnemiesDefeated();
  }

  private void SpawnInitialEnemies()
  {
    foreach (Transform spawnPoint in spawnPoints)
    {
      for (int i = 0; i < maxEnemiesPerSpawnPoint; i++)
      {
        SpawnEnemy(spawnPoint);
        this.total_enemies += 1;
      }
    }
  }

  private IEnumerator SpawnNewEnemiesCoroutine()
  {
    hordeNumber++;
    playerObject.GetComponent<CombatController>().playerStats.hordeNumber = hordeNumber;
    yield return new WaitForSeconds(3f);

    foreach (Transform spawnPoint in spawnPoints)
    {
      SpawnEnemy(spawnPoint);
    }

    areSpawning = false;
    areEnemiesDefeated = false;


    CheckIfEnemiesDefeated();
  }

  private void SpawnEnemy(Transform spawnPoint)
  {
    GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
    EnemyController enemyController = enemy.GetComponent<EnemyController>();

    enemyController.player = playerObject;
    enemyController.enemyStats.hordeNumber = hordeNumber;
  }

  private int GetEnemyCount()
  {
    EnemyController[] enemies = FindObjectsOfType<EnemyController>();
    return enemies.Length;
  }

  private void CheckIfEnemiesDefeated()
  {
    if (GetEnemyCount() == 0)
    {
      areEnemiesDefeated = true;
      this.mean_death_time /= this.total_enemies;
      this.total_enemies = 0;
    }
  }

  private void OnDestroy()
  {
    StopAllCoroutines();
  }
}
