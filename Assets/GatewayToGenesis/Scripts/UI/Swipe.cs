using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
public class Swipe : MonoBehaviour, IBeginDragHandler, IEndDragHandler,IDragHandler 
{

    public GameObject card;
    public Vector2 pos;
    public TMP_Text status;
    public  DialogueManager dialogueManager;
    // Start is called before the first frame update

    public void OnBeginDrag(PointerEventData eventData)
    {
         pos=card.transform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        
        card.transform.position= Input.mousePosition;

        if (card.transform.position.x > pos.x + 200)
        {
            status.text = "right";

        }
        else if (card.transform.position.x < pos.x - 200)
        {
            status.text = "left";
        }
        else if (card.transform.position.y < pos.y - 200)
        {
            status.text = "down";
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        
        {
            if (card.transform.position.x > pos.x + 200)
            {
                dialogueManager.MakeChoice(0);
                card.transform.position = pos;

               
            }
            else if (card.transform.position.x < pos.x - 200)
            {
                dialogueManager.MakeChoice(1);
                card.transform.position = pos;
            }
            else if (card.transform.position.y < pos.y - 200)
            {
                dialogueManager.MakeChoice(2);
                card.transform.position = pos;
            }
            else
            {
                card.transform.position = pos;
            }
        }
        

    }

    void Start()
    {
        
    }

  

}
