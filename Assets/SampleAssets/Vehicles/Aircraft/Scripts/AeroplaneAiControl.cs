using System;
using System.Collections.Generic;
using TETCSharpClient;
using TETCSharpClient.Data;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace UnitySampleAssets.Vehicles.Aeroplane
{
    [RequireComponent(typeof (AeroplaneController))]
    public class AeroplaneAiControl : MonoBehaviour, IGazeListener
    {
        // This script represents an AI 'pilot' capable of flying the plane towards a designated target.
        // It sends the equivalent of the inputs that a user would send to the Aeroplane controller.
        [SerializeField] private float m_RollSensitivity = .001f;         // How sensitively the AI applies the roll controls
        [SerializeField] private float m_PitchSensitivity = .001f;        // How sensitively the AI applies the pitch controls
        [SerializeField] private float m_LateralWanderDistance = 5;     // The amount that the plane can wander by when heading for a target
        [SerializeField] private float m_LateralWanderSpeed = 0.11f;    // The speed at which the plane will wander laterally
        //[SerializeField] private float m_MaxClimbAngle = 45;            // The maximum angle that the AI will attempt to make plane can climb at
        //[SerializeField] private float m_MaxRollAngle = 45;             // The maximum angle that the AI will attempt to u
        [SerializeField] private float m_MaxClimbAngle = 105;            // The maximum angle that the AI will attempt to make plane can climb at
        [SerializeField] private float m_MaxRollAngle = 105;             // The maximum angle that the AI will attempt to u
        [SerializeField] private float m_SpeedEffect = 0.01f;           // This increases the effect of the controls based on the plane's speed.
        [SerializeField] private float m_TakeoffHeight = 20;            // the AI will fly straight and only pitch upwards until reaching this height
        [SerializeField] private Transform m_Target;                    // the target to fly towards

        private AeroplaneController m_AeroplaneController;  // The aeroplane controller that is used to move the plane
        private float m_RandomPerlin;                       // Used for generating random point on perlin noise so that the plane will wander off path slightly
        private bool m_TakenOff;                            // Has the plane taken off yet

        private GameObject _EyeTarget;
        private Camera _Camera;

        public delegate void Callback();
        private Queue<Callback> _CallbackQueue;

        // setup script properties
        private void Awake()
        {
            // get the reference to the aeroplane controller, so we can send move input to it and read its current state.
            m_AeroplaneController = GetComponent<AeroplaneController>();

            // pick a random perlin starting point for lateral wandering
            m_RandomPerlin = Random.Range(0f, 100f);

            _Camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();

            _EyeTarget = GameObject.FindGameObjectWithTag("waypoint");
            SetTarget(_EyeTarget.transform);

            //init call back queue
            _CallbackQueue = new Queue<Callback>();

            //register for gaze updates
            GazeManager.Instance.AddGazeListener(this);

#if !UNITY_EDITOR
            //hide waypoint if not in editor
            _EyeTarget.GetComponent<MeshRenderer>().enabled = false;
#endif
        }

        // reset the object to sensible values
        public void Reset()
        {
            m_TakenOff = false;
        }

        private void Update()
        {
            /* @TheEyeTribe Check for collision and position waypoint */ 
            Point2D gazeCoords = GazeDataValidator.Instance.GetLastValidSmoothedGazeCoordinates();

            if (null != gazeCoords)
            {
                // Map gaze indicator
                Point2D gp = UnityGazeUtils.GetGazeCoordsToUnityWindowCoords(gazeCoords);

                Vector3 screenPoint = new Vector3((float)gp.X, (float)gp.Y, _Camera.nearClipPlane + .1f);

                // Handle collision detection
                Vector3 hitPoint;
                if (checkGazeCollision(screenPoint, out hitPoint))
                {
                    //do nothing
                }
            }
            else
            {
                // Use mouse position for hover effect as default
                Vector3 hitPoint;
                if (checkGazeCollision(Input.mousePosition, out hitPoint))
                {
                    //do nothing
                }
            }

            /* @TheEyeTribe Handling callback queue on main thread */ 
            lock (_CallbackQueue)
            {
                //we handle queued callback in the update loop
                while (_CallbackQueue.Count > 0)
                    _CallbackQueue.Dequeue()();
            }
        }

        // fixed update is called in time with the physics system update
        private void FixedUpdate()
        {
            if (m_Target != null)
            {
                // make the plane wander from the path, useful for making the AI seem more human, less robotic.
                /*
                Vector3 targetPos = m_Target.position +
                                    transform.right*
                                    (Mathf.PerlinNoise(Time.time*m_LateralWanderSpeed, m_RandomPerlin)*2 - 1)*
                                    m_LateralWanderDistance;
                 */
                Vector3 targetPos = m_Target.position;

                // adjust the yaw and pitch towards the target
                Vector3 localTarget = transform.InverseTransformPoint(targetPos);
                float targetAngleYaw = Mathf.Atan2(localTarget.x, localTarget.z);
                float targetAnglePitch = -Mathf.Atan2(localTarget.y, localTarget.z);

                // Set the target for the planes pitch, we check later that this has not passed the maximum threshold
                targetAnglePitch = Mathf.Clamp(targetAnglePitch, -m_MaxClimbAngle*Mathf.Deg2Rad,
                                               m_MaxClimbAngle*Mathf.Deg2Rad);

                // calculate the difference between current pitch and desired pitch
                float changePitch = targetAnglePitch - m_AeroplaneController.PitchAngle;

                // AI always applies gentle forward throttle
                const float throttleInput = 0.5f;

                // AI applies elevator control (pitch, rotation around x) to reach the target angle
                float pitchInput = changePitch*m_PitchSensitivity;

                // clamp the planes roll
                float desiredRoll = Mathf.Clamp(targetAngleYaw, -m_MaxRollAngle*Mathf.Deg2Rad, m_MaxRollAngle*Mathf.Deg2Rad);
                float yawInput = 0;
                float rollInput = 0;
                if (!m_TakenOff)
                {
                    // If the planes altitude is above m_TakeoffHeight we class this as taken off
                    if (m_AeroplaneController.Altitude > m_TakeoffHeight)
                    {
                        m_TakenOff = true;
                    }
                }
                else
                {
                    // now we have taken off to a safe height, we can use the rudder and ailerons to yaw and roll
                    yawInput = targetAngleYaw;
                    rollInput = -(m_AeroplaneController.RollAngle - desiredRoll)*m_RollSensitivity;
                }

                // adjust how fast the AI is changing the controls based on the speed. Faster speed = faster on the controls.
                float currentSpeedEffect = 1 + (m_AeroplaneController.ForwardSpeed*m_SpeedEffect);
                rollInput *= currentSpeedEffect;
                pitchInput *= currentSpeedEffect;
                yawInput *= currentSpeedEffect;

                // pass the current input to the plane (false = because AI never uses air brakes!)
                m_AeroplaneController.Move(rollInput, pitchInput, yawInput, throttleInput, false);
            }
            else
            {
                // no target set, send zeroed input to the planeW
                m_AeroplaneController.Move(0, 0, 0, 0, false);
            }
        }

        // allows other scripts to set the plane's target
        public void SetTarget(Transform target)
        {
            m_Target = target;
        }

        #region The Eye Tribe

        private const float WAYPOINT_DISTANCE = 130f;
        private Collider _CurrentHit;

        public void OnGazeUpdate(GazeData gazeData)
        {
            //Handle on main UI thread
            QueueCallback(new Callback(delegate
            {
                Invoke("PositionWayPoint", 0);
            }));
        }

        private void PositionWayPoint() 
        {
            if (null != _CurrentHit)
            {
                // position based on Gazable target if not too close

                Vector3 targetVec = _CurrentHit.transform.position - transform.position;
                double distance = Math.Abs(targetVec.magnitude);

                if (distance > 40)
                {
                    Vector3 newTarget = _CurrentHit.transform.position;

                    // We adjust target vertically to force pitch
                    float yDiff = _CurrentHit.transform.position.y - transform.position.y;
                    newTarget.y += yDiff;

                    _EyeTarget.transform.position = newTarget;
                    SetTarget(_EyeTarget.transform);
                    return;
                }
            }

            //position based on gaze

            if (m_TakenOff)
            {
                Point2D gazeCoords = GazeDataValidator.Instance.GetLastValidSmoothedGazeCoordinates();

                Vector3 planeCoord = Vector3.zero;
                if (null != gazeCoords)
                {
                    // Map gaze to Unity space
                    Point2D gp = UnityGazeUtils.GetGazeCoordsToUnityWindowCoords(gazeCoords);
                    Vector3 screenPoint = new Vector3((float)gp.X, (float)gp.Y, WAYPOINT_DISTANCE);

                    planeCoord = _Camera.ScreenToWorldPoint(screenPoint);

                    _EyeTarget.transform.position = planeCoord;
                    SetTarget(_EyeTarget.transform);
                }
            }
        }

        /// <summary>
        /// Intersect the current gaze with the 3D model, and return the intersection coordinate.
        /// </summary>
        private bool checkGazeCollision(Vector3 screenPoint, out Vector3 hitPoint)
        {
            hitPoint = new Vector3();

            Ray collisionRay = _Camera.ScreenPointToRay(screenPoint);
            RaycastHit hit;

            if (Physics.Raycast(collisionRay, out hit))
            {
                //switch colors of cubes according to collision state
                if (_CurrentHit != hit.collider)
                {
                    if (hit.collider.tag.Equals("Gazable"))
                    {
                        _CurrentHit = hit.collider;

                        hitPoint = hit.point;
                    }
                    else
                        _CurrentHit = null;
                }

                return true;
            }
            else
            {
                _CurrentHit = null;
            }

            return false;
        }

        /// <summary>
        /// Utility method for adding callback tasks to a queue
        /// that will eventually be handle in the Unity game loop 
        /// method 'Update()'.
        /// </summary>
        public void QueueCallback(Callback newTask)
        {
            lock (_CallbackQueue)
            {
                _CallbackQueue.Enqueue(newTask);
            }
        }

        public void ExitButtonPress()
        {
            Application.Quit();
        }

        public void RecalibrateButtonPress()
        {
            Application.LoadLevel(0);
        }
        #endregion
    }
}
