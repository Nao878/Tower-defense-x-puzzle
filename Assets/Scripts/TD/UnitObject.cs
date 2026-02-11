using UnityEngine;

/// <summary>
/// フィールド上のユニットコンポーネント
/// 射程内の敵を自動攻撃するAIを持つ
/// </summary>
public class UnitObject : MonoBehaviour
{
    [Header("ステータス")]
    [SerializeField] private float range = 3f;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private float damage = 10f;

    [Header("ビジュアル")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform firePoint;

    [Header("攻撃エフェクト（任意）")]
    [SerializeField] private GameObject attackEffectPrefab;

    private float attackTimer;
    private Transform currentTarget;
    private UnitData unitData;

    /// <summary>
    /// UnitDataからパラメータを初期化する
    /// </summary>
    public void Initialize(UnitData data)
    {
        unitData = data;
        range = data.range;
        attackSpeed = data.attackSpeed;
        damage = data.damage;

        if (bodyRenderer != null && data.icon != null)
        {
            bodyRenderer.sprite = data.icon;
        }

        gameObject.name = $"Unit_{data.unitName}";
    }

    private void Update()
    {
        attackTimer += Time.deltaTime;

        // ターゲットの有効性チェック
        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.position);
            if (dist > range || !currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = null;
            }
        }

        // ターゲットがなければ探す
        if (currentTarget == null)
        {
            FindTarget();
        }

        // 攻撃
        if (currentTarget != null && attackTimer >= attackSpeed)
        {
            Attack();
            attackTimer = 0f;
        }
    }

    /// <summary>
    /// 射程内の最も近い敵を探す
    /// </summary>
    private void FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        float closestDist = float.MaxValue;
        Transform closest = null;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = hit.transform;
                }
            }
        }

        currentTarget = closest;
    }

    /// <summary>
    /// 現在のターゲットを攻撃する
    /// </summary>
    private void Attack()
    {
        if (currentTarget == null) return;

        EnemyController enemy = currentTarget.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            Debug.Log($"[Unit] {gameObject.name} が {currentTarget.name} に {damage} ダメージ");
        }

        // 攻撃エフェクト
        if (attackEffectPrefab != null)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Instantiate(attackEffectPrefab, spawnPos, Quaternion.identity);
        }
    }

    /// <summary>
    /// デバッグ用：射程範囲をGizmoで表示
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
