﻿/*
 * Copyright (c) 2013-present, The Eye Tribe. 
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree. 
 *
 */

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TETCSharpClient;
using TETCSharpClient.Data;

class UnityGazeUtils : GazeUtils
{
    /// <summary>
    /// Converts a coordinate on picture space to a 3D pose, using an expected inter-eyes distance to compute depth coordinate
    /// </summary>
    public static Point3D BackProjectDepth(Point2D eyePictCoord, double eyesDistance, double baseDist)
    {

        //mapping cam panning to 3:2 aspect ratio
        double tx = (eyePictCoord.X * 5) - 2.5f;
        double ty = (eyePictCoord.Y * 3) - 1.5f;

        //position camera X-Y plane and adjust distance
        double depthMod = 2 * eyesDistance;

        return new Point3D((float)tx,
                           (float)ty,
                           (float)(baseDist + depthMod));
    }

    /// <summary>
    /// Converts a coordinate on picture space to a 3D pose, using an expected inter-eyes distance to compute depth coordinate.
    /// We follow the standard Pinhole model here
    /// </summary>
    public static Point3D BackProjectDepthPinhole(Point2D eyePictCoord, double pictEyesDistance)
    {
        // We use the pinhole model, with a depth related to the inter-eyes distance
        double interEyesDistance = 0.06; 	// 6cm on average
        double depth = interEyesDistance / Math.Max(pictEyesDistance, 0.0001F);

        double tx = (eyePictCoord.X - 0.5) * depth;
        double ty = (eyePictCoord.Y - 0.5) * depth;

        return new Point3D((float)tx,
                           (float)ty,
                           (float)depth);
    }


    /// <summary>
    /// Maps a GazeData gaze point (RawCoordinates or SmoothedCoordinates) to Unity screen space. 
    /// Note that gaze points have origo in top left corner, whilst Unity uses lower left.
    /// </summary>
    /// <param name="gp"/>gaze point to map</param>
    /// <returns>2d point mapped to unity window space or null if input null</returns>
    public static Point2D GetGazeCoordsToUnityWindowCoords(Point2D gp)
    {
        if (null != gp)
        { 
            double rx = gp.X * ((double)Screen.width / GazeManager.Instance.ScreenResolutionWidth);
            double ry = (GazeManager.Instance.ScreenResolutionHeight - gp.Y) * ((double)Screen.height / GazeManager.Instance.ScreenResolutionHeight);

            return new Point2D(rx, ry);
        }

        return null;
    }

    /// <summary>
    /// Convert a Point2D to Unity vector.
    /// </summary>
    /// <param name="gp"/>gaze point to convert</param>
    /// <returns>a vector representation of point</returns>
    public static Vector2 Point2DToVec2(Point2D gp)
    {
        return new Vector2((float)gp.X, (float)gp.Y);
    }

    /// <summary>
    /// Convert a Unity Vector3 to a double[].
    /// </summary>
    /// <param name="gp"/>Vector to convert</param>
    /// <returns>double array</returns>
    public static double[] Vec3ToArray(Vector3 vec)
    {
        return new double[3] { vec.x, vec.y, vec.z };
    }

    /// <summary>
    /// Convert a double[3] to a Unity Vector.
    /// </summary>
    /// <param name="gp"/>Array to convert</param>
    /// <returns>Unity Vector3</returns>
    public static Vector3 ArrayToVec3(double[] array)
    {
        return new Vector3((float)array[0], (float)array[1], (float)array[2]);
    }

    /// <summary>
    /// Converts a relative point to screen point in pixels using Unity classes
    /// </summary>
    public static Point2D GetRelativeToScreenSpace(Point2D gp)
    {
        return GetRelativeToScreenSpace(gp, Screen.width, Screen.height);
    }

    /// <summary>
    /// Converts a screen point in pixels to normalized relative values based on 
    /// EyeTribe Server screen settings
    /// </summary>
    public static Point2D GetScreenSpaceToRelative(Point2D gp)
    {
        return new Point2D(gp.X / GazeManager.Instance.ScreenResolutionWidth, gp.Y / GazeManager.Instance.ScreenResolutionHeight);
    }

    /// <summary>
    /// Converts a screen point in pixels to normalized values relative to screen 
    /// center based on EyeTribe Server screen settings
    /// </summary>
    public static Point2D GetScreenToRelativeCenter(Point2D gp)
    {
        Point2D rel = GetScreenSpaceToRelative(gp);

        Debug.Log("ScreenSpace: " + rel.ToString());

        rel.X = (rel.X * 2) - 1;
        rel.Y = (rel.Y * 2) - 1;

        Debug.Log("Relative: " + rel.ToString());

        return rel; 
    }
}
