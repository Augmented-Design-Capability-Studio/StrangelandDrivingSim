/*
 * Copyright (C) 2016, Jaguar Land Rover
 * This program is licensed under the terms and conditions of the
 * Mozilla Public License, version 2.0.  The full text of the
 * Mozilla Public License is at https://www.mozilla.org/MPL/2.0/
 */


using System;
using Unity.Netcode;
using UnityEngine;
using System.Collections;


public class NetworkVehicleController : NetworkBehaviour {
    public Transform CameraPosition;

    private VehicleController controller;
    public bool useKeyBoard;


    public Transform[] Left;
    public Transform[] Right;
    public Transform[] BrakeLightObjects;
    public Material baseMaterial;

    private Material materialOn;
    private Material materialOff;
    private Material materialBrake;


    public Color BrakeColor;
    public Color On;
    public Color Off;
    private AudioSource HonkSound;
    public float SteeringInput;
    public float ThrottleInput;

    [HideInInspector] public float selfAlignmentTorque;
    private ulong CLID;

    private SteeringWheelInputController steeringInput;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if (IsServer) {
            controller = GetComponent<VehicleController>();
            steeringInput  = GetComponent<SteeringWheelInputController>();
        }
    }

    private void Start() {
        indicaterStage = 0;
        //Generating a new material for on/off;
        materialOn = new Material(baseMaterial);
        materialOn.SetColor("_Color", On);
        materialOff = new Material(baseMaterial);
        materialOff.SetColor("_Color", Off);
        materialBrake = new Material(baseMaterial);
        materialBrake.SetColor("_Color", BrakeColor);


        HonkSound = GetComponent<AudioSource>();

        foreach (Transform t in Left) { t.GetComponent<MeshRenderer>().material = materialOff; }

        foreach (Transform t in Right) { t.GetComponent<MeshRenderer>().material = materialOff; }

        foreach (Transform t in BrakeLightObjects) { t.GetComponent<MeshRenderer>().material = materialOff; }
    }


    [ClientRpc]
    public void TurnOnLeftClientRpc(bool Leftl_) {
        if (Leftl_) {
            foreach (Transform t in Left) { t.GetComponent<MeshRenderer>().material = materialOn; }
        }
        else {
            foreach (Transform t in Left) { t.GetComponent<MeshRenderer>().material = materialOff; }
        }
    }

    [ClientRpc]
    public void TurnOnRightClientRpc(bool Rightl_) {
        if (Rightl_) {
            foreach (Transform t in Right) { t.GetComponent<MeshRenderer>().material = materialOn; }
        }
        else {
            foreach (Transform t in Right) { t.GetComponent<MeshRenderer>().material = materialOff; }
        }
    }

    [ClientRpc]
    public void TurnOnBrakeLightClientRpc(bool Active) {
        if (Active) {
            foreach (Transform t in BrakeLightObjects) { t.GetComponent<MeshRenderer>().material = materialBrake; }
        }
        else {
            foreach (Transform t in BrakeLightObjects) { t.GetComponent<MeshRenderer>().material = materialOff; }
        }
    }


    [ClientRpc]
    public void HonkMyCarClientRpc() {
        Debug.Log("HonkMyCarClientRpc");
        HonkSound.Play();
    }

    private void LateUpdate() {
        if (IsServer && controller != null) {
            controller.steerInput = SteeringInput;
            controller.accellInput = ThrottleInput;
        }
    }

    private void OnGUI() {
        GUI.Box(new Rect(200, 100, 300, 30), "IsServer: " + IsServer.ToString() + "  IsHost: " + IsHost.ToString() +
                                             "  IsClient: " +
                                             IsClient.ToString());
    }
    float steeringAngle;
    public Transform SteeringWheel;
    void Update() {
        if (!IsServer) return;
        if (ConnectionAndSpawing.Singleton.ServerState == ActionState.DRIVE) {
                if (steeringInput == null || useKeyBoard) {
                    SteeringInput = Input.GetAxis("Horizontal");
                    ThrottleInput = Input.GetAxis("Vertical");
                }
                else {
                    SteeringInput = steeringInput.GetSteerInput();
                    ThrottleInput = steeringInput.GetAccelInput();
                    SteeringWheel.RotateAround(SteeringWheel.position, SteeringWheel.up,
                        steeringAngle - SteeringInput * -450f);
                    steeringAngle =SteeringInput * -450f;
                }

                bool TempLeft = Input.GetButton("indicateLeft");
                bool TempRight = Input.GetButton("indicateRight");
                if (TempLeft || TempRight) {
                    DualButtonDebounceIndicator = true;
                    if (TempLeft) { LeftIndicatorDebounce = true; }

                    if (TempRight) { RightIndicatorDebounce = true; }
                }
                else if (DualButtonDebounceIndicator && !TempLeft && !TempRight) {
                    startBlinking(LeftIndicatorDebounce, RightIndicatorDebounce);
                    DualButtonDebounceIndicator = false;
                    LeftIndicatorDebounce = false;
                    RightIndicatorDebounce = false;
                }

                UpdateIndicator();


                if (Input.GetButtonDown("Horn")) { HonkMyCarServerRpc(); }

                if (ThrottleInput < 0 && !breakIsOn) {
                    BrakeLightChangedServerRpc(true);
                    breakIsOn = true;
                }
                else if (ThrottleInput >= 0 && breakIsOn) {
                    BrakeLightChangedServerRpc(false);
                    breakIsOn = false;
                }
            }
            else if (ConnectionAndSpawing.Singleton.ServerState == ActionState.QUESTIONS) {
                SteeringInput = 0;
                ThrottleInput = -1;
            }
        
    }

    public void AssignClient(ulong CLID_) {
        if (IsServer) {
            NetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            CLID = CLID_;
            SetPlayerParent(CLID_);
        }
        else { Debug.LogWarning("Tried to execute something that should never happen. "); }
    }


    private void SetPlayerParent(ulong clientId) {
        if (IsSpawned && IsServer) {
            // As long as the client (player) is in the connected clients list
            if (NetworkManager.ConnectedClients.ContainsKey(clientId)) {
                // Set the player as a child of this in-scene placed NetworkObject 
                NetworkManager.ConnectedClients[clientId].PlayerObject.transform.parent =
                    transform; // Should be Camera position but this doesnt work cause of NetworkObject restrictions
                NetworkManager.SceneManager.OnSceneEvent -= SceneManager_OnSceneEvent;
                
            }
        }
    }

    private void SceneManager_OnSceneEvent(SceneEvent sceneEvent) {
        Debug.Log("SceneManager_OnSceneEvent called with event:" + sceneEvent.SceneEventType.ToString());
        switch (sceneEvent.SceneEventType) {
            case SceneEventType.SynchronizeComplete: {
                Debug.Log("Scene event change by Client: " + sceneEvent.ClientId);
                if (sceneEvent.ClientId == CLID) {
                    Debug.Log("Server: " + IsServer.ToString() + "  IsClient: " + IsClient.ToString() +
                              "  IsHost: " + IsHost.ToString());
                    SetPlayerParent(sceneEvent.ClientId);
                }

                break;
            }
            default:
                break;
        }
    }
    
    
    private bool breakIsOn;
    [ServerRpc]
    private void BrakeLightChangedServerRpc(bool newvalue) { TurnOnBrakeLightClientRpc(newvalue); }
    
    [ServerRpc]
    public void HonkMyCarServerRpc() {
        Debug.Log("HonkMyCarServerRpc");
        HonkMyCarClientRpc();
    }
    
    #region IndicatorLogic
    
    
    private bool LeftActive;
    private bool RightActive;


    private bool LeftIsActuallyOn;
    private bool RightIsActuallyOn;
    private bool ActualLightOn;
    private float indicaterTimer;
    public float interval;
    private int indicaterStage;

    private bool DualButtonDebounceIndicator;
    private bool LeftIndicatorDebounce;
    private bool RightIndicatorDebounce;
    
    

    void startBlinking(bool left, bool right) {
        indicaterStage = 1;
        if (left == right == true) {
            if (LeftIsActuallyOn != true || RightIsActuallyOn != true) {
                LeftIsActuallyOn = true;
                RightIsActuallyOn = true;
            }
            else if (LeftIsActuallyOn == RightIsActuallyOn == true) {
                RightIsActuallyOn = false;
                LeftIsActuallyOn = false;
            }
        }

        if (left != right) {
            if (LeftIsActuallyOn == RightIsActuallyOn ==
                true) // When we are returning from the hazard lights we make sure that not the inverse thing turns on 
            {
                LeftIsActuallyOn = false;
                RightIsActuallyOn = false;
            }

            if (left) {
                if (!LeftIsActuallyOn) {
                    LeftIsActuallyOn = true;
                    RightIsActuallyOn = false;
                }
                else { LeftIsActuallyOn = false; }
            }

            if (right) {
                if (!RightIsActuallyOn) {
                    LeftIsActuallyOn = false;
                    RightIsActuallyOn = true;
                }
                else { RightIsActuallyOn = false; }
            }
        }
    }


    void UpdateIndicator() {
        if (indicaterStage == 1) {
            indicaterStage = 2;
            indicaterTimer = interval;
            ActualLightOn = false;
        }
        else if (indicaterStage == 2 || indicaterStage == 3) {
            indicaterTimer += Time.deltaTime;

            if (indicaterTimer > interval) {
                indicaterTimer = 0;
                ActualLightOn = !ActualLightOn;
                if (ActualLightOn) {
                    LeftIndicatorChanged(LeftIsActuallyOn);
                    RightIndicatorChanged(RightIsActuallyOn);
                }
                else {
                    RightIndicatorChanged(false);
                    LeftIndicatorChanged(false);
                }
            }

            if (indicaterStage == 2) {
                if (steeringInput != null && Mathf.Abs(steeringInput.GetSteerInput() * -450f) > 90) {
                    indicaterStage = 3;
                }
            }
            else if (indicaterStage == 3) {
                if (steeringInput != null && Mathf.Abs(steeringInput.GetSteerInput() * -450f) < 10) {
                    indicaterStage = 4;
                }
            }
        }
        else if (indicaterStage == 4) {
            indicaterStage = 0;
            ActualLightOn = false;
            LeftIsActuallyOn = false;
            RightIsActuallyOn = false;
            RightIndicatorChanged(false);
            LeftIndicatorChanged(false);
            // UpdateIndicatorLightsServerRpc(false, false);
        }
    }
    private void RightIndicatorChanged(bool newvalue) {TurnOnRightClientRpc(newvalue); }

    private void LeftIndicatorChanged(bool newvalue) { TurnOnLeftClientRpc(newvalue); }

    #endregion
}