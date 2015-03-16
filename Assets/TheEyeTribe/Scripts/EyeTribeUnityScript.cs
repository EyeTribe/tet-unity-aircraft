/*
 * Copyright (c) 2013-present, The Eye Tribe. 
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree. 
 *
 */

using UnityEngine;
using System.Collections;
using TETCSharpClient;
using TETCSharpClient.Data;

public class EyeTribeUnityScript : MonoBehaviour, IGazeListener
{
    private Camera _Camera;
    private GameObject _GazeIndicator;
    private bool _ShowGazeIndicator = true;

    void Start()
    {
        _Camera = GetComponentInChildren<Camera>();
        _GazeIndicator = GameObject.FindGameObjectWithTag("gazeIndicator");

        //activate C# TET client, default port
        GazeManager.Instance.Activate
        (
            GazeManager.ApiVersion.VERSION_1_0,
            GazeManager.ClientMode.Push
        );

        //register for gaze updates
        GazeManager.Instance.AddGazeListener(this);
    }

    public void OnGazeUpdate(GazeData gazeData)
    {
        //Add frame to GazeData cache handler
        GazeDataValidator.Instance.Update(gazeData);
    }

    void Update()
    {
        Point2D gazeCoords = GazeDataValidator.Instance.GetLastValidSmoothedGazeCoordinates();

        Vector3 planeCoord = Vector3.zero;
        if (null != gazeCoords)
        {
            // Map gaze indicator
            Point2D gp = UnityGazeUtils.GetGazeCoordsToUnityWindowCoords(gazeCoords);

            Vector3 screenPoint = new Vector3((float)gp.X, (float)gp.Y, _Camera.nearClipPlane + .1f);

            planeCoord = _Camera.ScreenToWorldPoint(screenPoint);
            _GazeIndicator.transform.position = planeCoord;
        }

        if (_ShowGazeIndicator && !_GazeIndicator.activeSelf)
            _GazeIndicator.SetActive(true);
        else if (!_ShowGazeIndicator && _GazeIndicator.activeSelf)
            _GazeIndicator.SetActive(false);
    }

    public void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }

        if (Input.GetButton("Cancel"))
        {
            Application.Quit();
        }
        
        if (Input.GetKey(KeyCode.F1))
        {
            _ShowGazeIndicator = !_ShowGazeIndicator;
        }

        if (Input.GetKey(KeyCode.F1))
        {
            _ShowGazeIndicator = !_ShowGazeIndicator;
        }
    }

    void OnApplicationQuit()
    {
        GazeManager.Instance.RemoveGazeListener(this);
        GazeManager.Instance.Deactivate();
    }
    public void GazeIndicatorButtonPress()
    {
        _ShowGazeIndicator = !_ShowGazeIndicator;
    }
}
