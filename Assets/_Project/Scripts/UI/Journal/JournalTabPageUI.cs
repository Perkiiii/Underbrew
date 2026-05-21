using UnityEngine;

public abstract class JournalTabPageUI : MonoBehaviour
{
    public virtual bool CanRenderPage => true;

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);

        if (visible)
        {
            OnShown();
            RefreshPage();
        }
        else
        {
            OnHidden();
        }
    }

    public void RefreshPage()
    {
        if (!gameObject.activeInHierarchy)
            return;

        OnRefreshPage();
    }

    protected virtual void OnShown()
    {
    }

    protected virtual void OnHidden()
    {
    }

    protected virtual void OnRefreshPage()
    {
    }
}
