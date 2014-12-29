using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class KCameraController : MonoBehaviour
{
    public static KCameraController instance;

    private Transform origParent = null;
    private Quaternion origRotation = Quaternion.identity;
    private Vector3 origPosition = Vector3.zero;
    private float origFov;
    private float origClip;

    private MuMechModuleHullCamera activeLocalCamera;
    private Vessel activeRemoteCamera;

    public void Awake()
    {
        instance = this;
        GameEvents.onVesselWillDestroy.Add(checkRemoteCamera);
        GameEvents.onGameSceneLoadRequested.Add(onSceneChange);
    }

    public void OnDestroy()
    {
        RestoreMainCamera();
        GameEvents.onVesselWillDestroy.Remove(checkRemoteCamera);
        instance = null;
    }

    public void onSceneChange(GameScenes scene)
    {
        RestoreMainCamera();
    }
    
    private void SaveMainCamera()
    {
        FlightCamera cam = FlightCamera.fetch;
        origParent = cam.transform.parent;
        origClip = Camera.main.nearClipPlane;
        origFov = Camera.main.fieldOfView;
        origPosition = cam.transform.localPosition;
        origRotation = cam.transform.localRotation;
    }

    private void RestoreMainCamera()
    {
        if (origParent == null)
            return;
        FlightCamera cam = FlightCamera.fetch;
        cam.transform.parent = origParent;
        cam.transform.localPosition = origPosition;
        cam.transform.localRotation = origRotation;
        Camera.main.nearClipPlane = origClip;
        cam.SetFoV(origFov);

        if (FlightGlobals.ActiveVessel != null && HighLogic.LoadedScene == GameScenes.FLIGHT)
        {
            cam.setTarget(FlightGlobals.ActiveVessel.transform);
        }
        origParent = null;
        if (activeLocalCamera != null)
            activeLocalCamera.part.OnJustAboutToBeDestroyed -= findCamera;
        activeLocalCamera = null;
        activeRemoteCamera = null;
    }

    public void activateLocalCamera(MuMechModuleHullCamera camera)
    {
        activeRemoteCamera = null;

        if (activeLocalCamera != null)
            activeLocalCamera.part.OnJustAboutToBeDestroyed -= findCamera;
        activeLocalCamera = camera;
        activeLocalCamera.part.OnJustAboutToBeDestroyed += findCamera;

        ScreenMessages.PostScreenMessage("Viewing from local camera", 5.0f, ScreenMessageStyle.UPPER_LEFT);
    }

    public void activateRemoteCamera(Vessel vessel)
    {
        activeRemoteCamera = vessel;

        if (activeLocalCamera != null)
            activeLocalCamera.part.OnJustAboutToBeDestroyed -= findCamera;
        activeLocalCamera = null;

        ScreenMessages.PostScreenMessage("Viewing from: " + vessel.GetName(), 5.0f, ScreenMessageStyle.UPPER_LEFT);
    }

    private void findCamera()
    {
        List<MuMechModuleHullCamera> cameras = findLocalCameras(FlightGlobals.ActiveVessel);
        if (cameras.Count > 0)
        {
            activateLocalCamera(cameras[0]);
            return;
        }

        //If no camera could be found, set the normal main camera (fallback scenario)
        if (activeLocalCamera)
            RestoreMainCamera();
    }

    public void nextLocalCamera()
    {
        List<MuMechModuleHullCamera> cameras = findLocalCameras(FlightGlobals.ActiveVessel);
        if (cameras.Count < 1)
            return;
        if (activeLocalCamera == null)
        {
            activateLocalCamera(cameras[0]);
        }
        else
        {
            activateLocalCamera(cameras[(cameras.IndexOf(activeLocalCamera) + 1) % cameras.Count]);
        }
    }

    public void nextRemoteCamera()
    {
        List<Vessel> cameras = findRemoteCameras();
        if (cameras.Count < 1)
            return;
        if (activeRemoteCamera == null)
        {
            activateRemoteCamera(cameras[0]);
        }
        else
        {
            activateRemoteCamera(cameras[(cameras.IndexOf(activeRemoteCamera) + 1) % cameras.Count]);
        }
    }

    public void previousLocalCamera()
    {
        List<MuMechModuleHullCamera> cameras = findLocalCameras(FlightGlobals.ActiveVessel);
        if (cameras.Count < 1)
            return;
        if (activeLocalCamera == null)
        {
            activateLocalCamera(cameras[0]);
        }
        else
        {
            activateLocalCamera(cameras[(cameras.IndexOf(activeLocalCamera) + cameras.Count - 1) % cameras.Count]);
        }
    }

    public void previousRemoteCamera()
    {
        List<Vessel> cameras = findRemoteCameras();
        if (cameras.Count < 1)
            return;
        if (activeRemoteCamera == null)
        {
            activateRemoteCamera(cameras[0]);
        }
        else
        {
            activateRemoteCamera(cameras[(cameras.IndexOf(activeRemoteCamera) + cameras.Count - 1) % cameras.Count]);
        }
    }

    private void checkRemoteCamera(Vessel v)
    {
        if (activeRemoteCamera == v)
            activeRemoteCamera = null;
    }

    private bool vesselHasModule(Vessel v, string module_name)
    {
        //MuMechModuleHullCameraZoom
        if (v.loaded)
        {
            foreach (Part p in v.parts)
            {
                if (p.State != PartStates.DEAD)
                {
                    foreach (PartModule pm in p.Modules)
                        if (pm.moduleName == module_name)
                            return true;
                }
            }
        }else{
            foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
            {
                foreach (ProtoPartModuleSnapshot m in pps.modules)
                    if (m.moduleName == module_name)
                        return true;
            }
        }
        return false;
    }

    private List<MuMechModuleHullCamera> findLocalCameras(Vessel v)
    {
        List<MuMechModuleHullCamera> cameras = new List<MuMechModuleHullCamera>();
        if (v != null)
        {
            foreach (Part p in v.parts)
            {
                if (p.State != PartStates.DEAD)
                {
                    foreach (PartModule pm in p.Modules)
                        if (pm is MuMechModuleHullCamera)
                            cameras.Add(pm as MuMechModuleHullCamera);
                }
            }
        }
        return cameras;
    }

    private List<Vessel> findRemoteCameras()
    {
        List<Vessel> list = new List<Vessel>();
        foreach (Vessel v in FlightGlobals.Vessels)
        {
            if (v != FlightGlobals.ActiveVessel && !v.loaded && checkPlanetLoS(FlightGlobals.ActiveVessel.CoM, v.CoM) && vesselHasModule(v, "MuMechModuleHullCameraZoom"))
                list.Add(v);
        }
        return list;
    }

    private bool checkPlanetLoS(Vector3d start, Vector3d end)
    {
        return checkPlanetLoS(start, end, Planetarium.fetch.Sun);
    }

    private bool checkPlanetLoS(Vector3d start, Vector3d end, CelestialBody b)
    {
        double mag = (end - start).magnitude;
        double f = Vector3d.Dot(end - start, b.position - start) / mag;
        f = Math.Max(0.0f, Math.Min(mag, f));
        Vector3d q = start + (end - start) / mag * f;
        if ((q - b.position).magnitude < b.Radius)
            return false;
        foreach (CelestialBody bb in b.orbitingBodies)
            if (!checkPlanetLoS(start, end, bb))
                return false;
        return true;
    }

    void Update()
    {
        if (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Flight)
            return;
        
        if (activeLocalCamera != null)
        {
            if (activeLocalCamera.vessel != FlightGlobals.ActiveVessel || activeLocalCamera.part.State == PartStates.DEAD)
            {
                findCamera();
            }
            else
            {
                if (origParent == null)
                    SaveMainCamera();

                FlightCamera cam = FlightCamera.fetch;
                cam.setTarget(null);
                cam.transform.parent = (activeLocalCamera.cameraTransformName.Length > 0) ? activeLocalCamera.part.FindModelTransform(activeLocalCamera.cameraTransformName) : activeLocalCamera.part.transform;
                cam.transform.localPosition = activeLocalCamera.cameraPosition;
                cam.transform.localRotation = Quaternion.LookRotation(activeLocalCamera.cameraForward, activeLocalCamera.cameraUp);
                cam.SetFoV(activeLocalCamera.cameraFoV);
                Camera.main.nearClipPlane = activeLocalCamera.cameraClip;
            }
        }
        else if (activeRemoteCamera != null)
        {
            RestoreMainCamera();

            if (FlightGlobals.ActiveVessel == null || activeRemoteCamera.loaded || activeRemoteCamera.state == Vessel.State.DEAD || !checkPlanetLoS(FlightGlobals.ActiveVessel.CoM, activeRemoteCamera.CoM))
            {
                findCamera();
            }
            else
            {
                FlightCamera.fetch.SetCamCoordsFromPosition(activeRemoteCamera.CoM);
                FlightCamera.fetch.SetDistanceImmediate(100.0f);
            }
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
            nextLocalCamera();
        if (Input.GetKeyDown(KeyCode.O))
            nextRemoteCamera();
        if (Input.GetKeyDown(KeyCode.P) && origParent != null)
            RestoreMainCamera();
    }
}

public class MuMechModuleHullCamera : PartModule
{
    [KSPField]
    public Vector3 cameraPosition = Vector3.zero;

    [KSPField]
    public Vector3 cameraForward = Vector3.forward;

    [KSPField]
    public Vector3 cameraUp = Vector3.up;

    [KSPField]
    public string cameraTransformName = "";

    [KSPField]
    public float cameraFoV = 60;

    [KSPField(isPersistant = false)]
    public float cameraClip = 0.01f;

    [KSPField]
	public bool camActive = false; // Saves when we're viewing from this camera.

    [KSPField]
	public bool camEnabled = true; // Lets us skip cycling through cameras.

    [KSPField(isPersistant = false)]
    public string cameraName = "Hull";
}
