using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using MyEnums;

public class QEnemyAgent : Agent
{
  private float episodeTime;
  public GameObject player;
  private EnemyController enemyController;
  private CombatController playerCombatController;

  private float[][] QTable; // QTable from the Agent
                            // QTable: filas (i) = estado, columnas (j) = acción
  private float learningRate = 0.1f; // Learning Rate
  private float discountFactor = 0.99f; // Discount Factor
  private int state;
  private int action;
  float rewardPerEpisode = 0f;

  public override void Initialize()
  {
    enemyController = GetComponent<EnemyController>();
    playerCombatController = enemyController.player.GetComponent<CombatController>();

    // Inicializar la QTable con valores aleatorios
    int numStates = 2; // Número de estados
    int numActions = 3; // Número de acciones
    QTable = new float[numStates][];
    for (int i = 0; i < numStates; i++)
    {
      QTable[i] = new float[numActions];
      for (int j = 0; j < numActions; j++)
      {
        QTable[i][j] = Random.Range(-1f, 1f);
      }
    }
  }

  public override void OnEpisodeBegin()
  {
    // RESET ENEMY ATTRS
    enemyController.transform.position = new Vector3(0f, 0f, 15f);
    enemyController.enemyStats.health = 100;
    enemyController.healthBar.value = 100;
    enemyController.enemyStats.accuracy = 0;
    enemyController.isAgent = true;
    enemyController.player = player;

    // RESET PLAYER ATTRS
    playerCombatController.playerStats.health = 100;
    playerCombatController.healthBar.value = 100;
    playerCombatController.isTraining = true;

    // RESET LOGIC
    Debug.Log($"AgentId {enemyController.enemyStats.id} rewardPerEpisode -> {rewardPerEpisode}");
    rewardPerEpisode = 0f;
    episodeTime = 0f;
  }

  public override void CollectObservations(VectorSensor sensor)
  {
    // Observaciones del entorno y del agente
    Vector3 playerPosition = playerCombatController.GetPosition();
    Vector3 enemyPosition = transform.position;

    // 1. Distancia entre el jugador y el enemigo
    float distanceToPlayer = Vector3.Distance(playerPosition, enemyPosition);
    sensor.AddObservation(distanceToPlayer);

    // 2. Diferencia entre la vida del jugador y el enemigo
    sensor.AddObservation(playerCombatController.playerStats.health);
    sensor.AddObservation(enemyController.enemyStats.health);

    // 3. Puntaje del jugador
    float playerScore = playerCombatController.GetMainMetric();
    sensor.AddObservation(playerScore);

    // 4. Precisión del enemigo
    float enemyAccuracy = enemyController.GetMainMetric();
    sensor.AddObservation(enemyAccuracy);

    // 5. Posición del jugador
    sensor.AddObservation(playerPosition);
  }

  public override void OnActionReceived(ActionBuffers actions)
  {
    //Tiempo del episodio
    episodeTime += Time.deltaTime;
    // Acciones continuas del agente
    float moveX = actions.ContinuousActions[0];
    float moveZ = actions.ContinuousActions[1];

    // Acciones discretas del agente
    int state = actions.DiscreteActions[0]; // State: isMoving, isAttacking
    int action = actions.DiscreteActions[1]; // Action: attackRange, chasingRange, speedRange
    int attackRange = actions.DiscreteActions[2];
    int chasingRange = actions.DiscreteActions[3];
    int speedRange = actions.DiscreteActions[4];

    if (state == 0)
    {
      enemyController.enemyAnimator.SetBool("isMoving", true);
      if (Vector3.Distance(transform.position, player.transform.position) >= enemyController.chasingRange)
      {
        transform.position += new Vector3(moveX, 0f, moveZ) * (Time.deltaTime * 1f);
      }
    }
    else if (state == 1)
    {
      enemyController.enemyAnimator.SetBool("isMoving", false);
      enemyController.Attack();
    }

    switch (action)
    {
      case 0:
        ModifyAgentCharacteristics(attackRange, -1, -1);
        break;
      case 1:
        ModifyAgentCharacteristics(-1, chasingRange, -1);
        break;
      case 2:
        ModifyAgentCharacteristics(-1, -1, speedRange);
        break;
    }

    // Actualizar el estado y la acción previa
    int previousState = state;
    int previousAction = action;

    // Calcular la recompensa en base al cambio de estado
    float reward = CalculateReward();
    // Actualizar la QTable
    QTable[previousState][previousAction] += learningRate * (reward + discountFactor * GetMaxQValue(state) - QTable[previousState][previousAction]);
    reward += GetMaxQValue(state);
    rewardPerEpisode += reward;
    SetReward(reward);
  }

  private bool CheckMountainCollision()
  {
    Collider[] colliders = Physics.OverlapSphere(transform.position, 1f);
    foreach (Collider collider in colliders)
    {
      if (collider.CompareTag("Mountain"))
      {
        return true;
      }
    }
    return false;
  }

  private float CalculateReward()
  {
    float reward = 0f;
    // Obtener la distancia entre el enemigo y el jugador
    Vector3 playerPosition = playerCombatController.GetPosition();
    Vector3 enemyPosition = transform.position;
    float distanceToPlayer = Vector3.Distance(playerPosition, enemyPosition);

    // Calcular la recompensa basada en la diferencia entre la distancia actual y la distancia objetivo
    if (distanceToPlayer <= 14f)
    {
      reward += 14f / distanceToPlayer;
      AddReward(14f / distanceToPlayer);
    }
    else
    {
      // El enemigo está lejos del jugador (comportamiento no deseado)
      reward += distanceToPlayer * -0.1f;
      AddReward(distanceToPlayer * -0.1f);
    }

    // Verificar colisiones con montañas
    if (CheckMountainCollision())
    {
      // Aplicar recompensa negativa y finalizar el episodio
      reward += -100f;
      AddReward(-100f);
      EndEpisode();
    }

    // SI EL JUGADOR SE MUERE
    if (playerCombatController.playerStats.health <= 0)
    {
      // SI EL JUGADOR SE MUERE MUY RÁPIDO
      if (episodeTime <= 30f)
      {
        reward += -10f;
        AddReward(-10f);
      }
      // SI EL JUGADOR SE MUERE EN UN INTERVALO ACEPTABLE
      else if (30f < episodeTime && episodeTime <= 180f)
      {
        reward += 10f;
        AddReward(10f);
      }
      // SI EL JUGADOR SE DEMORA EN MORIR
      else if (180f < episodeTime)
      {
        reward += 10f;
        AddReward(10f);
      }
      EndEpisode();
    }

    // Enemy's health rewards
    if (enemyController.enemyStats.health <= 0)
    {
      // IF the enemy died too fast
      if (episodeTime <= 30f)
      {
        reward += -10f;
        AddReward(-10f);
      }
      reward += -10f;
      AddReward(-10f);

      EndEpisode();
    }

    // Attack rewards
    switch (enemyController._attackState)
    {
      case AttackStates.SUCCESS:
        reward += 5f;
        AddReward(5f);
        enemyController._attackState = AttackStates.NO_ATTACK;
        break;
      case AttackStates.FAIL:
        reward += -10f;
        AddReward(-10f);
        enemyController._attackState = AttackStates.NO_ATTACK;
        break;
      default:
        break;
    }

    if (episodeTime >= 210f && playerCombatController.playerStats.health > 0)
    {
      reward += -30f;
      AddReward(-30f);
      EndEpisode();
    }

    return reward;
  }

  private void ModifyAgentCharacteristics(int attackRange, int chasingRange, int speedRange)
  {
    // Configurar el rango de ataque del enemigo
    switch (attackRange)
    {
      case 0:
        enemyController.attackRange = 1f;
        break;
      case 1:
        enemyController.attackRange = 1.5f;
        break;
      case 2:
        enemyController.attackRange = 2f;
        break;
      case 3:
        enemyController.attackRange = 2.5f;
        break;
      case 4:
        enemyController.attackRange = 3f;
        break;
    }

    // Configurar el rango de persecución del enemigo
    switch (chasingRange)
    {
      case 0:
        enemyController.chasingRange = 6f;
        break;
      case 1:
        enemyController.chasingRange = 8f;
        break;
      case 2:
        enemyController.chasingRange = 10f;
        break;
      case 3:
        enemyController.chasingRange = 12f;
        break;
      case 4:
        enemyController.chasingRange = 14f;
        break;
    }

    // Configurar la velocidad del enemigo
    switch (speedRange)
    {
      case 0:
        enemyController.speedRange = 0.006f;
        break;
      case 1:
        enemyController.speedRange = 0.008f;
        break;
      case 2:
        enemyController.speedRange = 0.009f;
        break;
      case 3:
        enemyController.speedRange = 0.011f;
        break;
      case 4:
        enemyController.speedRange = 0.013f;
        break;
    }
  }


  private float GetMaxQValue(int state)
  {
    // Obtener el valor máximo de la QTable para el estado dado
    float maxQValue = Mathf.Max(QTable[state]);
    return maxQValue;
  }

  public override void Heuristic(in ActionBuffers actionsOut)
  {
    // No hacer nada
  }
}
