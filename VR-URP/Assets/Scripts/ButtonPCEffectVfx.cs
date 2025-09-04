using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonPCEffectVfx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public GameObject targetObject;
    public GameObject standardObject;
    public GameObject holoObject;
    public Button triggerButton;            
    public Color normalColor = Color.white; 
    public Color hoverColor = Color.green;  
    public Color clickColor = Color.blue;   
    public Color activeColor = Color.red;   

    private Image buttonImage;
    private bool isActive = false;           

    void Start()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.AddListener(ToggleTargetObject);
            buttonImage = triggerButton.GetComponent<Image>();  
            buttonImage.color = normalColor;  
        }
    }

    
    private void ToggleTargetObject()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(true);
            standardObject.SetActive(false);
            holoObject.SetActive(false);

        }

        
        isActive = !isActive;

        // Met à jour la couleur en fonction de l'état actif ou inactif
        if (isActive)
        {
            buttonImage.color = activeColor;  
        }
        else
        {
            buttonImage.color = normalColor;  
        }
    }

    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActive)  
        {
            buttonImage.color = hoverColor;
        }
    }

    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isActive)  
        {
            buttonImage.color = normalColor;
        }
    }

    
    public void OnPointerClick(PointerEventData eventData)
    {
        
    }
}
