using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Parallax : MonoBehaviour
{
    [SerializeField] private Vector2 movSpeed;
    [SerializeField] private Camera cam;

    private Vector2 offset;
    private Material material;

    private void Awake()
    {
        material = GetComponent<Renderer>().material;
    }

    // Update is called once per frame
    void Update()
    {
        offset = (cam.velocity.x * 0.1f) * movSpeed * Time.deltaTime;
        material.mainTextureOffset += offset;
    }
}
