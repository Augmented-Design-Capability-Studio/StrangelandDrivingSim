using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VRUIManager : MonoBehaviour {
    [Header("UI Elements")] public GameObject UIPrefab;

    public GameObject UIcanvas;

    public TMP_Dropdown ParticipantDropdown;
    public TMP_Text ParticipantText;
    public TMP_Dropdown LanguageDropdown;
    public TMP_Text LanguageText;
    public TMP_Dropdown SpawnTypeDropdown;
    public TMP_Text SpawnTypeText;
    public TMP_Text ServerIPText;

    public TMP_Text ServerConnectionStatusText;
    public Button saveConfigButton;
    public Button repositionCanvasButton;

    [Header("Config")] public string defaultServerIP = "192.168.";

    public string fileName = "ParticipantConfig.json";

    [Header("Debug")] public bool deleteSaveFile;

    public bool useDebug;
    public bool usePremadeConfig;

    private ParticipantConfig currentConfig;
    private bool loadConfigSuccess;

    private void Start() {
        if (deleteSaveFile) File.Delete(Application.persistentDataPath + "/" + fileName);

        PopulateUI();
        TryLoadConfiguration();
        StartCoroutine(MoveCanvasAfterDelay());

        if (loadConfigSuccess) {
            AutoStartStudy();
        }
        else if (usePremadeConfig) {
            currentConfig = new ParticipantConfig();
            currentConfig.ParticipantIDString = "C";
            currentConfig.LanguageString = "English";
            currentConfig.SpawnTypeString = "Pedestrian";
            currentConfig.ServerIPString = "192.168.0.103";
            AutoStartStudy();
        }
    }

    private void Update() {
        // debug log the current config if it exists
        if (currentConfig.ParticipantIDString != null && useDebug)
            Debug.Log(
                $"Current Config: {currentConfig.ParticipantIDString}, {currentConfig.LanguageString}, {currentConfig.SpawnTypeString}, {currentConfig.ServerIPString}");
    }

    private IEnumerator MoveCanvasAfterDelay() {
        yield return new WaitForSeconds(0.05f);
        MoveInFrontOfCamera();
    }

    private void MoveInFrontOfCamera() {
        // move UI in front of camera
        var cameraPos = Camera.main.transform.position;
        var cameraForward = Camera.main.transform.forward;
        var cameraUp = Camera.main.transform.up;
        var newPos = cameraPos + cameraForward * 0.5f + cameraUp * -0.15f;
        UIcanvas.transform.position = newPos;

        // look at camera by only rotating on y axis
        var lookPos = new Vector3(Camera.main.transform.position.x, UIcanvas.transform.position.y,
            Camera.main.transform.position.z);
        UIcanvas.transform.LookAt(lookPos);
        UIcanvas.transform.Rotate(0, 180, 0);
    }

    private void AutoStartStudy() {
        ServerConnectionStatusText.text = "Trying to establish a connection";
        Debug.Log(
            $"Log: AutoStart {currentConfig.LanguageString}, {currentConfig.ParticipantIDString}, {currentConfig.ServerIPString}, {currentConfig.SpawnTypeString}");
        ConnectionAndSpawning.Singleton.StartAsClient(currentConfig.LanguageString,
            StringToEnum<ParticipantOrder>(currentConfig.ParticipantIDString),
            currentConfig.ServerIPString,
            7777,
            ResponseDelegate,
            StringToEnum<SpawnType>(currentConfig.SpawnTypeString)
        );
        ServerConnectionStatusText.text = "Waiting for Connection Response";
    }

    public static T StringToEnum<T>(string value) where T : struct, Enum {
        if (Enum.TryParse(value, true, out T result)) return result;

        throw new ArgumentException($"Cannot convert \"{value}\" to enum of type {typeof(T).Name}.");
    }

    private void ResponseDelegate(ConnectionAndSpawning.ClientConnectionResponse response) {
        switch (response) {
            case ConnectionAndSpawning.ClientConnectionResponse.FAILED:
                Debug.Log("Connection Failed maybe change IP address, participant order (A,b,C, etc.) or the port");
                ServerConnectionStatusText.text = "Connection failed";
                break;
            case ConnectionAndSpawning.ClientConnectionResponse.SUCCESS:
                Debug.Log("We are connected you can stop showing the UI now!");
                ServerConnectionStatusText.text = "CONNECTED!";

                Destroy(UIPrefab);
                break;
        }
    }

    #region UI

    private void PopulateUI() {
        saveConfigButton.onClick.AddListener(delegate { SaveConfiguration(); });
        repositionCanvasButton.onClick.AddListener(delegate { MoveInFrontOfCamera(); });

        PopulateDropdown<ParticipantOrder>(ParticipantDropdown);
        PopulateDropdown<Language>(LanguageDropdown);
        PopulateDropdown<SpawnType>(SpawnTypeDropdown);

        // set text field to default value
        ServerIPText.text = "IP: " + defaultServerIP;

        // set dropdowns to default values
        ParticipantDropdown.value = 0;
        LanguageDropdown.value = 0;
        SpawnTypeDropdown.value = 0;

        ParticipantDropdown.onValueChanged.AddListener(delegate {
            UpdateDropdownTextValue(ParticipantDropdown, ParticipantText, "ID: ",
                value => currentConfig.ParticipantIDString = value);
        });
        LanguageDropdown.onValueChanged.AddListener(delegate {
            UpdateDropdownTextValue(LanguageDropdown, LanguageText, "Lang: ",
                value => currentConfig.LanguageString = value);
        });
        SpawnTypeDropdown.onValueChanged.AddListener(delegate {
            UpdateDropdownTextValue(SpawnTypeDropdown, SpawnTypeText, "Spawn Type: ",
                value => currentConfig.SpawnTypeString = value);
        });
    }

    private void PopulateDropdown(TMP_Dropdown dropdown, List<string> items) {
        dropdown.options.Clear();
        foreach (var item in items) dropdown.options.Add(new TMP_Dropdown.OptionData { text = item });
    }

    private void PopulateDropdown<TEnum>(TMP_Dropdown dropdown) where TEnum : Enum {
        var enumNames = new List<string>(Enum.GetNames(typeof(TEnum)));
        PopulateDropdown(dropdown, enumNames);
    }

    private void UpdateDropdownTextValue(TMP_Dropdown dropdown, TMP_Text textBox, string prefix,
        Action<string> updateTargetField = null) {
        Debug.Log($"Updating dropdown value to {dropdown.value}");
        var index = dropdown.value;
        if (index >= 0 && index < dropdown.options.Count) {
            var value = dropdown.options[index].text;
            textBox.text = prefix + value;

            updateTargetField?.Invoke(value);
        }
    }

    public void AddTextToIPField(string text) {
        currentConfig.ServerIPString += text;
        ServerIPText.text = "IP: " + currentConfig.ServerIPString;
    }

    public void RemoveTextFromIPField() {
        if (currentConfig.ServerIPString.Length > 0) {
            currentConfig.ServerIPString = currentConfig.ServerIPString.Remove(currentConfig.ServerIPString.Length - 1);
            ServerIPText.text = "IP: " + currentConfig.ServerIPString;
        }
    }


    private void UpdateInputFieldTextValue(TMP_InputField inputField, TMP_Text textBox, string prefix,
        ref string targetField) {
        var value = inputField.text;
        textBox.text = prefix + value;
        targetField = value;
    }

    #endregion

    #region Config IO

    private void SaveConfiguration() {
        // prevent null ip from being saved
        if (currentConfig.ServerIPString == null) currentConfig.ServerIPString = "192.168.";

        var jsonString = JsonConvert.SerializeObject(currentConfig);
        File.WriteAllText(GetFilePath(), jsonString);
        Application.Quit();
    }

    private void TryLoadConfiguration() {
        if (File.Exists(GetFilePath())) {
            loadConfigSuccess = LoadConfiguration();
        }
        else {
            ParticipantDropdown.value = 0;
            LanguageDropdown.value = 0;
            SpawnTypeDropdown.value = 0;
            ServerIPText.text = "IP: " + defaultServerIP;
            currentConfig.ServerIPString = defaultServerIP;
            loadConfigSuccess = false;

            ServerConnectionStatusText.text = "No Config Found";
        }
    }

    private bool LoadConfiguration() {
        try {
            var jsonString = File.ReadAllText(GetFilePath());
            currentConfig = JsonConvert.DeserializeObject<ParticipantConfig>(jsonString);
        }
        catch (Exception e) {
            Console.WriteLine(e);
            return false;
        }

        // if any of the values are null, set them to default values
        if (currentConfig.ServerIPString == null) currentConfig.ServerIPString = "192.168.";

        ServerIPText.text = "IP: " + currentConfig.ServerIPString;

        // Update UI
        ParticipantDropdown.SetValueWithoutNotify(
            ParticipantDropdown.options.FindIndex(option => option.text == currentConfig.ParticipantIDString));
        LanguageDropdown.SetValueWithoutNotify(
            LanguageDropdown.options.FindIndex(option => option.text == currentConfig.LanguageString));
        SpawnTypeDropdown.SetValueWithoutNotify(
            SpawnTypeDropdown.options.FindIndex(option => option.text == currentConfig.SpawnTypeString));


        UpdateDropdownTextValue(ParticipantDropdown, ParticipantText, "ID: ");
        UpdateDropdownTextValue(LanguageDropdown, LanguageText, "Lang: ");
        UpdateDropdownTextValue(SpawnTypeDropdown, SpawnTypeText, "Spawn Type: ");
        return true;
    }

    private string GetFilePath() {
        return Application.persistentDataPath + "/" + fileName;
    }

    #endregion

    # region Keyboard

    #endregion
}