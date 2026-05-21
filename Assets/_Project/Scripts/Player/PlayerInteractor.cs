using UnityEngine;
using TMPro;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private TMP_Text promptTMPText;

    private Player player;
    private IInteractable currentInteractable;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        HidePrompt();
    }

    private void OnDisable()
    {
        currentInteractable = null;

        if (player != null)
            player.SetCurrentInteractable(null);

        HidePrompt();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        
        
        var interactable = GetInteractable(collision);
        if (interactable == null)
            return;

        currentInteractable = interactable;
        player.SetCurrentInteractable(currentInteractable);
        ShowPrompt(currentInteractable.PromptText);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        
        
        if (currentInteractable == null)
            return;

        var interactable = GetInteractable(collision);
        if (interactable == null)
            return;

        if (interactable != currentInteractable)
            return;

        currentInteractable = null;
        player.SetCurrentInteractable(null);
        HidePrompt();
    }

    private IInteractable GetInteractable(Collider2D collision)
    {
        var interactable = collision.GetComponent<IInteractable>();
        if (interactable != null)
            return interactable;

        interactable = collision.GetComponentInParent<IInteractable>();
        if (interactable != null)
            return interactable;

        return collision.GetComponentInChildren<IInteractable>();
    }

    private void ShowPrompt(string message)
    {
        if (promptTMPText != null)
        {
            promptTMPText.text = message;
            promptTMPText.gameObject.SetActive(true);
        }
    }

    private void HidePrompt()
    {
        if (promptTMPText != null)
            promptTMPText.gameObject.SetActive(false);
    }

}
