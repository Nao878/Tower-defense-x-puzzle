using System;
using UnityEngine;

/// <summary>
/// 敵の基本コンポーネント
/// ウェイポイントに沿って移動し、HPを管理する
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("ステータス")]
    [SerializeField] private float maxHp = 50f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("報酬")]
    [SerializeField] private int reward = 10;

    /// <summary>
    /// 敵が死亡したときのイベント
    /// </summary>
    public event Action<EnemyController> OnDeath;

    /// <summary>
    /// 敵がゴールに到達したときのイベント
    /// </summary>
    public event Action<EnemyController> OnReachGoal;

    private float currentHp;
    private Transform[] waypoints;
    private int currentWaypointIndex = 0;

    /// <summary>
    /// 現在のHP
    /// </summary>
    public float CurrentHp => currentHp;

    /// <summary>
    /// 最大HP
    /// </summary>
    public float MaxHp => maxHp;

    /// <summary>
    /// ウェイポイントを設定して初期化する
    /// </summary>
    public void Initialize(Transform[] waypointPath, float hp = -1f, float speed = -1f)
    {
        if (hp > 0) maxHp = hp;
        if (speed > 0) moveSpeed = speed;

        currentHp = maxHp;
        waypoints = waypointPath;
        currentWaypointIndex = 0;

        // タグの設定
        gameObject.tag = "Enemy";
    }

    private void Update()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        MoveAlongPath();
    }

    /// <summary>
    /// ウェイポイントに沿って移動する
    /// </summary>
    private void MoveAlongPath()
    {
        if (currentWaypointIndex >= waypoints.Length)
        {
            // ゴールに到達
            OnReachGoal?.Invoke(this);
            Destroy(gameObject);
            return;
        }

        Transform target = waypoints[currentWaypointIndex];
        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        // ウェイポイントに到達したか判定
        if (Vector2.Distance(transform.position, target.position) < 0.1f)
        {
            currentWaypointIndex++;
        }

        // 移動方向に応じてスプライトを反転
        if (direction.x != 0)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = direction.x < 0;
            }
        }
    }

    /// <summary>
    /// ダメージを受ける
    /// </summary>
    public void TakeDamage(float amount)
    {
        currentHp -= amount;

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// 死亡処理
    /// </summary>
    private void Die()
    {
        OnDeath?.Invoke(this);
        Debug.Log($"[Enemy] {gameObject.name} 撃破！");
        Destroy(gameObject);
    }
}
