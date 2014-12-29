using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MuMechModuleHullCameraZoom : MuMechModuleHullCamera
    {
        [KSPField]
        public float cameraFoVMax = 120;

        [KSPField]
        public float cameraFoVMin = 5;

        [KSPField]
        public float cameraZoomMult = 1.25f;
    }
}
