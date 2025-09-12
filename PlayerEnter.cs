using UnityEngine;
using TMPro;

public class PlayerEnter : MonoBehaviour
{
    public Camera PlayerCamera;
    public float InteractionDistance = 3f;
    public GameObject interactionText;
    private PlayerIntObject currentInteractable;

    void Update()
    {
        Ray ray = PlayerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, InteractionDistance))
        {
            PlayerIntObject interactable = hit.collider.GetComponent<PlayerIntObject>();
            if (interactable != null && interactable != currentInteractable)
            {
                currentInteractable = interactable;
                interactionText.SetActive(true);
                TextMeshProUGUI textComponent = interactionText.GetComponent<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = currentInteractable.GetInteractionText();
                }
            }
        }
        else
        {
            currentInteractable = null;
            interactionText.SetActive(false);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            currentInteractable?.Interact();
        }
    }
}
