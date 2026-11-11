using GC26.Tanki.Rules;
using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>The loading screen: the reactor charges while the player's tank circles it, then the tank fires.</summary>
    public sealed partial class WorldView
    {
        const float SplashRadius = 3f;
        const float SplashTurnRate = 1.9f;
        const float SplashChargeSeconds = 2.4f;
        const float SplashTurnSeconds = 0.25f;
        const float SplashShotSeconds = 0.55f;
        const float SplashTopAngle = Mathf.PI / 2f;

        enum SplashStep { Charging, Arriving, Turning, Firing, Done }

        public float SplashCharge { get; private set; }
        public float SplashFlash { get; private set; }
        public float SplashTitleShake { get; private set; }
        public bool SplashDone => splashStep == SplashStep.Done;

        /// <summary>True for the one frame the tank fires.</summary>
        public bool SplashJustFired { get; private set; }

        SplashStep splashStep;
        Transform splashRoot;
        Transform splashTank;
        Transform splashModel;
        Transform splashShot;
        Transform splashCore;
        Material splashCoreMaterial;
        Renderer[] splashArcs;
        float splashAngle;
        float splashTime;
        float splashStepTime;

        public void BeginSplash()
        {
            splashRoot = new GameObject("Splash").transform;
            splashRoot.SetParent(root, false);
            splashRoot.position = new Vector3(Levels.Width / 2f, 0f, Levels.Height / 2f);

            // Self-lit materials read brighter than their colour, so these are darker than they look.
            MarkerPart(splashRoot, RingMesh(0.001f, SplashRadius + 1.3f, 64), Vector3.zero, Hex("16111A"));
            MarkerPart(splashRoot, RingMesh(SplashRadius - 0.55f, SplashRadius + 0.55f, 64), new Vector3(0f, 0.005f, 0f), Hex("211A26"));

            var reactor = new GameObject("Reactor").transform;
            reactor.SetParent(splashRoot, false);
            reactor.localScale = Vector3.one * 1.7f;
            BuildReactor(reactor);
            splashCore = reactorCore;
            reactorCore = null;
            splashCoreMaterial = splashCore.GetComponent<Renderer>().material;

            // Three arcs on the floor light up as the charge grows, like the bar below.
            splashArcs = new Renderer[3];
            for (int i = 0; i < splashArcs.Length; i++)
            {
                Mesh arc = ArcMesh(1.25f, 1.5f, i * Mathf.PI * 2f / 3f + 0.08f, Mathf.PI * 2f / 3f - 0.16f, 24);
                splashArcs[i] = MarkerPart(splashRoot, arc, new Vector3(0f, 0.012f, 0f), HealthEmpty).GetComponent<Renderer>();
            }

            TankView tank = BuildTank(new Tank { IsPlayer = true });
            tank.MarkerRing.gameObject.SetActive(false);
            tank.HealthBar.gameObject.SetActive(false);
            tank.Shield.gameObject.SetActive(false);
            splashTank = tank.Root;
            splashModel = tank.Model;
            splashTank.SetParent(splashRoot, false);

            splashShot = Box(splashRoot, Vector3.zero, new Vector3(0.2f, 0.2f, 0.42f), Hex("FFE9A8"), true).transform;
            splashShot.gameObject.SetActive(false);

            // Starts where it will reach the top of the circle just as the charge fills.
            splashAngle = SplashTopAngle - SplashTurnRate * SplashChargeSeconds;
            splashStep = SplashStep.Charging;
        }

        public void AimSplashCamera(Camera camera)
        {
            Vector3 centre = new Vector3(Levels.Width / 2f, 0f, Levels.Height / 2f);
            camera.transform.rotation = Quaternion.Euler(CameraRig.TiltDegrees, 0f, 0f);
            float edge = SplashRadius + 1.4f;
            var corners = new[]
            {
                centre + new Vector3(-edge, 0f, 0f), centre + new Vector3(edge, 0f, 0f),
                centre + new Vector3(0f, 0f, -edge), centre + new Vector3(0f, 0f, edge)
            };
            for (float distance = 6f; distance < 60f; distance += 0.1f)
            {
                camera.transform.position = centre - camera.transform.forward * distance;
                bool fits = true;
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    if (point.x < 0.02f || point.x > 0.98f || point.y < 0.19f || point.y > 0.81f) fits = false;
                }
                if (fits) break;
            }
        }

        public void UpdateSplash(float dt)
        {
            splashTime += dt;
            splashStepTime += dt;
            SplashJustFired = false;

            SplashCharge = Mathf.Clamp01(splashTime / SplashChargeSeconds);
            ShowSplashCharge();

            switch (splashStep)
            {
                case SplashStep.Charging:
                case SplashStep.Arriving:
                    DriveSplashTank(dt);
                    if (splashStep == SplashStep.Charging && SplashCharge >= 1f) GoToSplashStep(SplashStep.Arriving);
                    if (splashStep == SplashStep.Arriving && Mathf.Abs(Mathf.DeltaAngle(splashAngle * Mathf.Rad2Deg, SplashTopAngle * Mathf.Rad2Deg)) < 14f)
                        GoToSplashStep(SplashStep.Turning);
                    break;

                case SplashStep.Turning:
                    splashTank.localRotation = Quaternion.RotateTowards(splashTank.localRotation, Quaternion.identity, 700f * dt);
                    if (splashStepTime >= SplashTurnSeconds)
                    {
                        splashTank.localRotation = Quaternion.identity;
                        splashShot.gameObject.SetActive(true);
                        splashShot.localPosition = splashTank.localPosition + new Vector3(0f, 0.5f, 1f);
                        SplashJustFired = true;
                        GoToSplashStep(SplashStep.Firing);
                    }
                    break;

                case SplashStep.Firing:
                    splashShot.localPosition += Vector3.forward * 16f * dt;
                    SplashFlash = Mathf.Max(0f, 1f - Mathf.Abs(splashStepTime - 0.3f) * 5f);
                    SplashTitleShake = Mathf.Max(0f, 1f - Mathf.Abs(splashStepTime - 0.28f) * 6f);
                    if (splashStepTime >= SplashShotSeconds) GoToSplashStep(SplashStep.Done);
                    break;
            }
        }

        public void EndSplash()
        {
            Kill(splashRoot.gameObject);
        }

        void GoToSplashStep(SplashStep step)
        {
            splashStep = step;
            splashStepTime = 0f;
        }

        void DriveSplashTank(float dt)
        {
            splashAngle += SplashTurnRate * dt;
            splashTank.localPosition = new Vector3(Mathf.Cos(splashAngle) * SplashRadius, 0f, Mathf.Sin(splashAngle) * SplashRadius);
            splashTank.localRotation = Quaternion.Euler(0f, -splashAngle * Mathf.Rad2Deg, 0f);
            splashModel.localPosition = new Vector3(0f, Mathf.Sin(splashTime * 30f) * 0.012f, 0f);
        }

        void ShowSplashCharge()
        {
            float third = 1f / splashArcs.Length;
            for (int i = 0; i < splashArcs.Length; i++)
            {
                bool lit = SplashCharge >= (i + 1) * third - 0.001f;
                splashArcs[i].sharedMaterial = MaterialFor(lit ? PlayerMark : HealthEmpty, true);
            }

            splashCoreMaterial.SetColor("_EmissionColor", ReactorGlow * (0.1f + SplashCharge * 1.1f));
            float size = 0.34f + SplashCharge * 0.1f + Mathf.Sin(splashTime * 8f) * 0.012f * SplashCharge;
            splashCore.localScale = new Vector3(size, 0.2f, size);
        }

        static Mesh ArcMesh(float inner, float outer, float start, float length, int segments)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = start + length * i / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;
                if (i == segments) break;

                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = (i + 1) * 2;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = (i + 1) * 2;
                triangles[t + 4] = (i + 1) * 2 + 1;
                triangles[t + 5] = i * 2 + 1;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
