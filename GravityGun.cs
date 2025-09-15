using UnityEngine;

public class GravityGun : MonoBehaviour
{
    [Header("References")]
    public Camera playerCam;
    public Transform holdPoint; // Empty GameObject in front of camera

    [Header("Settings")]
    public float pickupRange = 5f;
    public float holdForce = 150f;
    public float throwForce = 15f;
    public float maxPickupMass = 50f; // Objects heavier than this can’t be picked up

    private Rigidbody heldObject;
    private Outline currentOutline; // For highlighting

    void Update()
    {
        HandleHighlight();

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObject == null)
                TryPickup();
            else
                Drop();
        }

        if (Input.GetMouseButtonDown(0) && heldObject != null)
        {
            Throw();
        }
    }

    void FixedUpdate()
    {
        if (heldObject != null)
        {
            // Move object toward hold point
            Vector3 moveDirection = (holdPoint.position - heldObject.position);
            heldObject.velocity = moveDirection * holdForce * Time.fixedDeltaTime;

            // Rotate object with camera
            Quaternion targetRot = playerCam.transform.rotation;
            heldObject.MoveRotation(Quaternion.Slerp(heldObject.rotation, targetRot, Time.fixedDeltaTime * 10f));
        }
    }

    void TryPickup()
    {
        Ray ray = playerCam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange))
        {
            Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
            if (rb != null && rb.mass <= maxPickupMass)
            {
                heldObject = rb;
                heldObject.useGravity = false;
                heldObject.drag = 10;
                DisableHighlight();
            }
        }
    }

    void Drop()
    {
        if (heldObject != null)
        {
            heldObject.useGravity = true;
            heldObject.drag = 1;
            heldObject = null;
        }
    }

    void Throw()
    {
        if (heldObject != null)
        {
            // Scale throw force based on mass
            float massFactor = Mathf.Clamp01(1f - (heldObject.mass / maxPickupMass));
            float scaledForce = throwForce * (0.3f + 0.7f * massFactor); 
            // 30% of force minimum, 100% at very light objects

            heldObject.useGravity = true;
            heldObject.drag = 1;
            heldObject.AddForce(playerCam.transform.forward * scaledForce, ForceMode.VelocityChange);
            heldObject = null;
        }
    }

    void HandleHighlight()
    {
        // Remove old outline
        if (currentOutline != null)
        {
            currentOutline.enabled = false;
            currentOutline = null;
        }

        if (heldObject != null) return; // Don’t highlight if already holding

        Ray ray = playerCam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange))
        {
            Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
            if (rb != null && rb.mass <= maxPickupMass)
            {
                Outline outline = hit.collider.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = true;
                    currentOutline = outline;
                }
            }
        }
    }

    void DisableHighlight()
    {
        if (currentOutline != null)
        {
            currentOutline.enabled = false;
            currentOutline = null;
        }
    }
}
