/*
 * Copyright (C) 2016, Jaguar Land Rover
 * This program is licensed under the terms and conditions of the
 * Mozilla Public License, version 2.0.  The full text of the
 * Mozilla Public License is at https://www.mozilla.org/MPL/2.0/
 */

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class VehicleInputControllerNetworked : NetworkBehaviour {

    public Transform SteeringWheel;
    float steeringAngle;
    private VehicleController controller;
    private SteeringWheelInputController steeringInput;

    public bool useKeyBoard;
    float transitionlerp;

    public Transform[] Left;
    public Transform[] Right;
    public Transform[] BrakeLightObjects;
    public Material baseMaterial;

    public Material materialOn;
    public Material materialOff;
    public Material materialBrake;


    public Color BrakeColor;
    public Color On;
    public Color Off;

    public bool LeftActive;
    public bool RightActive;

    private bool breakIsOn = false;


    private bool ActualLightOn;
    private bool LeftIsActuallyOn;
    private bool RightIsActuallyOn;


    public float indicaterTimer;
    public float interval;
    public int indicaterStage;


    void Awake() {
        controller = GetComponent<VehicleController>();

    }
    private void Start() {
        if (SceneStateManager.Instance != null) {
            SceneStateManager.Instance.SetDriving();
        }
        steeringInput = GetComponent<SteeringWheelInputController>();
        indicaterStage = 0;

        //Generating a new material for on/off;
        materialOn = new Material(baseMaterial);
        materialOn.SetColor("_Color", On);
        materialOff = new Material(baseMaterial);
        materialOff.SetColor("_Color", Off);
        materialBrake = new Material(baseMaterial);
        materialBrake.SetColor("_Color", BrakeColor);


        foreach (Transform t in Left) {
            t.GetComponent<MeshRenderer>().material = materialOff;
        }
        foreach (Transform t in Right) {
            t.GetComponent<MeshRenderer>().material = materialOff;
        }
        foreach (Transform t in BrakeLightObjects) {
            t.GetComponent<MeshRenderer>().material = materialOff;
        }

    }
    void startBlinking(bool left) {
            indicaterStage = 1;
            if (left) {
                LeftIsActuallyOn = true;
                RightIsActuallyOn = false;
            } else {
                LeftIsActuallyOn = false;
                RightIsActuallyOn = true;
            }

    }

    void UpdateIndicator() {
        if (indicaterStage == 1) {
            indicaterStage = 2;
            indicaterTimer = 0;
            ActualLightOn = false;
        } else if (indicaterStage == 2 || indicaterStage == 3) {
            indicaterTimer += Time.deltaTime;

            if (indicaterTimer > interval) {
                indicaterTimer = 0;
                ActualLightOn = !ActualLightOn;
                if (ActualLightOn) {
                    CmdUpdateIndicatorLights(LeftIsActuallyOn, RightIsActuallyOn);
                } else {
                    CmdUpdateIndicatorLights(false, false);
                }
            }
            if (indicaterStage == 2) {

                if (Mathf.Abs(steeringInput.GetSteerInput() * -450f) > 90) {
                    indicaterStage = 3;
                }
            } else if (indicaterStage == 3) {
                if (Mathf.Abs(steeringInput.GetSteerInput() * -450f) < 10) {
                    indicaterStage = 4;
                }
            }

        } else if (indicaterStage == 4) {
            indicaterStage = 0;
            ActualLightOn = false;
            LeftIsActuallyOn = false;
            RightIsActuallyOn = false;
            CmdUpdateIndicatorLights(false, false);

        }
    }

    [Command]
    public void CmdUpdateIndicatorLights(bool Left, bool Right) {
        RpcTurnOnLeft(Left);
        RpcTurnOnRight(Right);

    }
    [ClientRpc]
    public void RpcTurnOnLeft(bool Leftl_) {
        if (Leftl_) {
            foreach (Transform t in Left) {
                t.GetComponent<MeshRenderer>().material = materialOn;
            }
        } else {
            foreach (Transform t in Left) {
                t.GetComponent<MeshRenderer>().material = materialOff;
            }
        }

    }
    [ClientRpc]
    public void RpcTurnOnRight(bool Rightl_) {
        if (Rightl_) {
            foreach (Transform t in Right) {
                t.GetComponent<MeshRenderer>().material = materialOn;
            }
        } else {
            foreach (Transform t in Right) {
                t.GetComponent<MeshRenderer>().material = materialOff;
            }
        }

    }

    [Command]
    public void CmdStartQuestionairGloablly() {
        RpcRunQuestionairNow();
    }

    [ClientRpc]
    public void RpcRunQuestionairNow() {
        FindObjectOfType<SpecificSceneManager>().runQuestionairNow();

    }
    [Command]
    public void CmdStartWalking() {
        RpcStartWallking();
    }

    [ClientRpc]
    public void RpcStartWallking() {
        foreach (MaleAvatarController a in FindObjectsOfType<MaleAvatarController>()) {
            a.ChooseAnimation(1);
        }
    }

    [Command]
    public void CmdSwitchBrakeLight(bool Active) {
        RpcTurnOnBrakeLight(Active);

    }
    [ClientRpc]
    public void RpcTurnOnBrakeLight(bool Active) {
        if (Active) {
            foreach (Transform t in BrakeLightObjects) {
                t.GetComponent<MeshRenderer>().material = materialBrake;
            }
        } else {
            foreach (Transform t in BrakeLightObjects) {
                t.GetComponent<MeshRenderer>().material = materialOff;
            }
        }

    }


    void Update() {



        

       

        if (Input.GetKeyUp(KeyCode.Space)) {
            foreach (seatCallibration sc in FindObjectsOfType<seatCallibration>()) {
                if (sc.isPartOfLocalPlayer()) {
                    sc.reCallibrate();
                    break;
                }
            }
        }


        if (isLocalPlayer && SceneStateManager.Instance.ActionState == ActionState.DRIVE) {

            if (Input.GetKeyDown(KeyCode.Q)){
                CmdStartQuestionairGloablly();
            }
            if (Input.GetKeyDown(KeyCode.A)) {
                GetComponentInChildren<GpsController>().SetDirection(GpsController.Direction.Right);
            }
            if (Input.GetKeyDown(KeyCode.D)) {
                GetComponentInChildren<GpsController>().SetDirection(GpsController.Direction.Left);
            }
            if (Input.GetKeyDown(KeyCode.W)) {
                GetComponentInChildren<GpsController>().SetDirection(GpsController.Direction.Straight);
            }

            if (Input.GetKeyDown(KeyCode.Y)) {
                CmdStartWalking();
            }

            transitionlerp = 0;
            if (steeringInput == null || useKeyBoard) {
                controller.steerInput = Input.GetAxis("Horizontal");
                controller.accellInput = Input.GetAxis("Vertical");
            } else {
                controller.steerInput = steeringInput.GetSteerInput();
                controller.accellInput = steeringInput.GetAccelInput();

                SteeringWheel.RotateAround(SteeringWheel.position, SteeringWheel.up, steeringAngle - steeringInput.GetSteerInput() * -450f);
                steeringAngle = steeringInput.GetSteerInput() * -450f;
            }
            if (Input.GetButtonDown("indicateLeft")) {
                Debug.Log("PushedIndicator Left");
                startBlinking(true);
            } else if (Input.GetButtonDown("indicateRight")) {
                Debug.Log("PushedIndicator Right");
                startBlinking(false);
            }
            UpdateIndicator();

            if (controller.accellInput < 0 && !breakIsOn) {
                breakIsOn = true;
                CmdSwitchBrakeLight(breakIsOn);
            } else if (controller.accellInput >= 0 && breakIsOn) {
                breakIsOn = false;
                CmdSwitchBrakeLight(breakIsOn);
            }

        } else if (isLocalPlayer && SceneStateManager.Instance.ActionState == ActionState.QUESTIONS) {

            if (transitionlerp < 1) {
                transitionlerp += Time.deltaTime * SceneStateManager.slowDownSpeed;
                controller.steerInput = Mathf.Lerp(controller.steerInput, 0, transitionlerp);
                controller.accellInput = Mathf.Lerp(0, -1, transitionlerp);
                Debug.Log("SteeringWheel: " + controller.steerInput + "\tAccel: " + controller.accellInput);
            }
        }
    }
}
