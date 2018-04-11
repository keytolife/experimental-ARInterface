using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace UnityARInterface
{
    public class AREditorInterface : ARInterface
    {
        float m_LastTime;
        enum State
        {
            Uninitialized,
            Initialized,
            WaitingToAddPlane1,
            WaitingToAddPlane2,
            Finished
        }

        State m_State;
        BoundedPlane[] m_FakePlanes;
        Pose m_CameraPose;
        bool m_WasMouseDownLastFrame;
        Vector3 m_LastMousePosition;
        Vector3 m_EulerAngles;
        Vector3[] m_PointCloud;

        Matrix4x4 m_DisplayTransform;
        WebCamTexture activeTexture;
        ARBackgroundRenderer m_BackgroundRenderer;

        void OnDestroy()
        {
            if (activeTexture != null)
            {
                activeTexture.Stop();
                GameObject.Destroy(activeTexture);
                activeTexture = null;
            }
        }

        public override IEnumerator StartService(Settings settings)
        {
            m_CameraPose = Pose.identity;
            m_CameraPose.position.Set(0, 0, 0);
            m_LastTime = Time.time;
            m_State = State.Initialized;
            m_FakePlanes = new BoundedPlane[2];
            m_FakePlanes[0] = new BoundedPlane()
            {
                id = "0x1",
                extents = new Vector2(2.2f, 2f),
                //extents = new Vector2(1f, 1f),
                rotation = Quaternion.AngleAxis(60f, Vector3.up),
                center = new Vector3(2f, -1f, 3f)
                //center = new Vector3(1f, 1f, 1f)
            };

            m_FakePlanes[1] = new BoundedPlane()
            {
                id = "0x2",
                extents = new Vector2(2f, 2f),
                rotation = Quaternion.AngleAxis(200f, Vector3.up),
                center = new Vector3(3f, 1f, 3f)
            };

            m_PointCloud = new Vector3[20];
            for (int i = 0; i < 20; ++i)
            {
                m_PointCloud[i] = new Vector3(
                    UnityEngine.Random.Range(-2f, 2f),
                    UnityEngine.Random.Range(-.5f, .5f),
                    UnityEngine.Random.Range(-2f, 2f));
            }

            IsRunning = true;
            return null;
        }

        public override void StopService()
        {
            m_BackgroundRenderer.backgroundMaterial = null;
            m_BackgroundRenderer.camera = null;
            m_BackgroundRenderer = null;
            IsRunning = false;

            UpdateCameraFeed();
        }

        public override bool TryGetUnscaledPose(ref Pose pose)
        {
            pose = m_CameraPose;
            return true;
        }

        public override bool TryGetCameraImage(ref CameraImage cameraImage)
        {
            return false;
        }

        public override void SetupCamera(Camera camera)
        {
            if (m_BackgroundRenderer == null)
            {
                m_BackgroundRenderer = new ARBackgroundRenderer();
                m_BackgroundRenderer.backgroundMaterial = Resources.Load("Materials/ARBackground", typeof(Material)) as Material;
                m_BackgroundRenderer.camera = camera;
                m_BackgroundRenderer.mode = ARRenderMode.StandardBackground;
            }
        }

        public override bool TryGetPointCloud(ref PointCloud pointCloud)
        {
            if (!IsRunning)
                return false;

            if (pointCloud.points == null)
                pointCloud.points = new List<Vector3>();

            pointCloud.points.Clear();
            pointCloud.points.AddRange(m_PointCloud);
            return true;
        }

        public override LightEstimate GetLightEstimate()
        {
            return new LightEstimate()
            {
                capabilities = LightEstimateCapabilities.None
            };
        }

        public override Matrix4x4 GetDisplayTransform()
        {
            if (activeTexture != null)
            {
                return m_DisplayTransform;
            }
            else
            {
                return Matrix4x4.identity;
            }
        }

        public override void UpdateCamera(Camera camera)
        {
            float speed = camera.transform.parent.localScale.x / 10f;
            float turnSpeed = 10f;
            var forward = m_CameraPose.rotation * Vector3.forward;
            var right = m_CameraPose.rotation * Vector3.right;
            var up = m_CameraPose.rotation * Vector3.up;

            if (Input.GetKey(KeyCode.W))
                m_CameraPose.position += forward * Time.deltaTime * speed;

            if (Input.GetKey(KeyCode.S))
                m_CameraPose.position -= forward * Time.deltaTime * speed;

            if (Input.GetKey(KeyCode.A))
                m_CameraPose.position -= right * Time.deltaTime * speed;

            if (Input.GetKey(KeyCode.D))
                m_CameraPose.position += right * Time.deltaTime * speed;

            if (Input.GetKey(KeyCode.Q))
                m_CameraPose.position += up * Time.deltaTime * speed;

            if (Input.GetKey(KeyCode.Z))
                m_CameraPose.position -= up * Time.deltaTime * speed;

            if (Input.GetMouseButton(1))
            {
                if (!m_WasMouseDownLastFrame)
                    m_LastMousePosition = Input.mousePosition;

                var deltaPosition = Input.mousePosition - m_LastMousePosition;
                m_EulerAngles.y += Time.deltaTime * turnSpeed * deltaPosition.x;
                m_EulerAngles.x -= Time.deltaTime * turnSpeed * deltaPosition.y;
                m_CameraPose.rotation = Quaternion.Euler(m_EulerAngles);
                m_LastMousePosition = Input.mousePosition;
                m_WasMouseDownLastFrame = true;
            }
            else
            {
                m_WasMouseDownLastFrame = false;
            }

            if (m_BackgroundRenderer != null)
            {
                var coords = new Vector2[]
                {
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(0, 0),
                    new Vector2(1, 0)
                };

                var transformMatrix = GetDisplayTransform();

                for (int i = 0; i < coords.Length; ++i)
                {
                    var transformedCoord = transformMatrix * (coords[i] - new Vector2(0.5f, 0.5f));
                    coords[i] = new Vector2(transformedCoord.x + 0.5f, transformedCoord.y + 0.5f);
                }

                m_BackgroundRenderer.backgroundMaterial.SetVector("_UvTopLeftRight", new Vector4(coords[0].x, coords[0].y, coords[1].x, coords[1].y));
                m_BackgroundRenderer.backgroundMaterial.SetVector("_UvBottomLeftRight", new Vector4(coords[2].x, coords[2].y, coords[3].x, coords[3].y));
            }
        }

        public override void Update()
        {
            switch (m_State)
            {
                case State.Initialized:
                    m_State = State.WaitingToAddPlane1;
                    m_LastTime = Time.time;
                    break;

                case State.WaitingToAddPlane1:

                    if (Time.time - m_LastTime > 1f)
                    {
                        OnPlaneAdded(m_FakePlanes[0]);
                        m_LastTime = Time.time;
                        m_State = State.WaitingToAddPlane2;
                    }
                    break;

                case State.WaitingToAddPlane2:

                    if (Time.time - m_LastTime > 1f)
                    {
                        OnPlaneAdded(m_FakePlanes[1]);
                        m_LastTime = Time.time;
                        m_State = State.Finished;
                    }
                    break;
            }

            UpdateCameraFeed();
        }

        void UpdateCameraFeed()
        {
            var originalActiveTexture = activeTexture;
            var devices = WebCamTexture.devices;

            if (activeTexture != null)
            {
                bool deviceExists = false;

                if (IsRunning)
                {
                    for (int i = 0; i < devices.Length; i++)
                    {
                        if (devices[i].name == devices[i].name)
                        {
                            deviceExists = true;
                            break;
                        }
                    }
                }

                if (!deviceExists)
                {
                    activeTexture.Stop();

                    GameObject.Destroy(activeTexture);
                    activeTexture = null;
                }
            }

            if ((activeTexture == null) && IsRunning)
            {
                int selectedDevice = -1;

                for (int i = 0; i < devices.Length; i++)
                {
                    if (selectedDevice == -1)
                    {
                        selectedDevice = i;
                    }

                    if (!devices[i].isFrontFacing)
                    {
                        break;
                    }
                }

                if (selectedDevice != -1)
                {
                    activeTexture = new WebCamTexture(devices[selectedDevice].name, Screen.width, Screen.height, Application.targetFrameRate);
                    activeTexture.Play();
                }
            }

            if (activeTexture != null)
            {
                m_DisplayTransform = Matrix4x4.Rotate(Quaternion.Euler(0.0f, 0.0f, 90.0f + activeTexture.videoRotationAngle)) *
                                     Matrix4x4.Scale(new Vector3(-1.0f, activeTexture.videoVerticallyMirrored ? -1.0f : 1.0f, 1.0f));

                if (m_BackgroundRenderer != null)
                {
                    m_BackgroundRenderer.backgroundMaterial.SetTexture("_MainTex", activeTexture);
                    m_BackgroundRenderer.mode = ARRenderMode.MaterialAsBackground;
                }
            }
            else
            {
                if (m_BackgroundRenderer != null)
                {
                    m_BackgroundRenderer.mode = ARRenderMode.StandardBackground;
                }
            }
        }
    }
}
