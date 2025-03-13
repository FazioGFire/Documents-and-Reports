using UnityEngine;

public class PAGravity : MonoBehaviour
{
    [SerializeField] public float gravityScale = 1;
    public bool isGravityEnabled = true;
    public Vector3 standardGravity = new Vector3(0, -9.81f, 0);

    void FixedUpdate()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if(rb != null && isGravityEnabled)
        {
            rb.AddForce(standardGravity * gravityScale, ForceMode.Acceleration);
        }
    }

    public void EnableGravity()
    {
        isGravityEnabled = true;
    }

    public void DisableGravity()
    {
        isGravityEnabled = false;
    }
}
