using UnityEngine;

public class PlayerProgression : MonoBehaviour
{
    float HP;
    float MP;
    float EXP;

    InventoryManager inv;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        inv = GetComponent<InventoryManager>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TakeDamage(float damage)
    {
        HP -= damage;
    }
}
