using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHoloEffectVfx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public GameObject targetObject;  // L'objet auquel ce bouton est lié
    public Button triggerButton;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.green;
    public Color activeColor = Color.blue;

    private Image buttonImage;
    private static GameObject activeObject;  // Référence à l'objet actuellement actif
    private static Button activeButton;      // Référence au bouton actuellement actif

    void Start()
    {
        buttonImage = triggerButton.GetComponent<Image>();
        buttonImage.color = normalColor;  // Initialisation de la couleur du bouton

        triggerButton.onClick.AddListener(ToggleTargetObject);  // Lors du clic sur le bouton
        UpdateButtonColor();  // Met à jour la couleur du bouton selon l'état de l'objet

        // Si un objet est déjà actif au démarrage, on le gère correctement
        if (targetObject.activeSelf)
        {
            activeObject = targetObject;  // Si cet objet est déjà actif, on le marque comme actif
            activeButton = triggerButton;  // Le bouton correspondant est celui actif
            buttonImage.color = activeColor;  // On le met en couleur active (bleue)
        }
    }

    // Méthode pour basculer l'état de l'objet lié
    private void ToggleTargetObject()
    {
        // Si cet objet est actif, on le désactive, sinon, on l'active
        if (targetObject.activeSelf)
        {
            targetObject.SetActive(false);  // Désactive l'objet
            if (activeObject == targetObject) activeObject = null;  // Si c'est l'objet actif, on le réinitialise
        }
        else
        {
            // Si un autre objet est déjà actif, on le désactive
            if (activeObject != null)
            {
                activeObject.SetActive(false);
                if (activeButton != null)
                {
                    activeButton.GetComponent<Image>().color = normalColor;  // Remet la couleur normale de l'autre bouton
                }
            }

            // Active l'objet référencé
            targetObject.SetActive(true);
            activeObject = targetObject;  // Marque cet objet comme actif
            activeButton = triggerButton; // Marque ce bouton comme celui actif
        }

        UpdateButtonColor();  // Met à jour la couleur du bouton actuel
    }

    // Mise à jour de la couleur du bouton en fonction de l'état de l'objet
    private void UpdateButtonColor()
    {
        if (targetObject.activeSelf)  // Si l'objet est actif
        {
            buttonImage.color = activeColor;  // Met la couleur active (bleue)
        }
        else
        {
            buttonImage.color = normalColor;  // Sinon, met la couleur normale (blanche)
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!targetObject.activeSelf)  // Si l'objet n'est pas actif, met la couleur de survol
        {
            buttonImage.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!targetObject.activeSelf)  // Si l'objet n'est pas actif, retourne à la couleur normale
        {
            buttonImage.color = normalColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Cette méthode est laissée vide si vous voulez ajouter des actions supplémentaires lors du clic
    }
}
