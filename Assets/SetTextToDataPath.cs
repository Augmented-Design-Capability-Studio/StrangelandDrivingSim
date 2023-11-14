using System.Collections;
using System.Collections.Generic;
using System.Net.Mime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SetTextToDataPath : MonoBehaviour {
    private Text m_textMeshPro;
    // Start is called before the first frame update
    void Start() {
        m_textMeshPro = GetComponent<Text>();
        m_textMeshPro.text = $"DataPath=\"{Application.persistentDataPath}\"";
    }

    // Update is called once per frame
   
}
