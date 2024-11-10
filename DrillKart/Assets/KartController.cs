using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Rendering.PostProcessing;
using Cinemachine;

public class KartController : MonoBehaviour
{
    private PostProcessVolume postVolume;
    private PostProcessProfile postProfile;

    public Transform kartModel;
    public Transform kartNormal;
    public Rigidbody sphere;

    public List<ParticleSystem> primaryParticles = new List<ParticleSystem>();
    public List<ParticleSystem> secondaryParticles = new List<ParticleSystem>();

    float speed, currentSpeed;
    float rotate, currentRotate;
    int driftDirection;
    float driftPower;
    int driftMode = 0;
    bool first, second, third;
    Color c;

    [Header("Bools")]
    public bool drifting;

    [Header("Parameters")]
    public float acceleration = 30f;
    public float steering = 80f;
    public float gravity = 10f;
    public LayerMask layerMask;

    [Header("Model Parts")]
    public Transform frontWheels;
    public Transform backWheels;
    public Transform steeringWheel;

    [Header("Particles")]
    public Transform wheelParticles;
    public Transform flashParticles;
    public Color[] turboColors;

    void Start()
    {
        postVolume = Camera.main.GetComponent<PostProcessVolume>();
        postProfile = postVolume.profile;

        for (int i = 0; i < wheelParticles.GetChild(0).childCount; i++)
        {
            primaryParticles.Add(wheelParticles.GetChild(0).GetChild(i).GetComponent<ParticleSystem>());
        }

        for (int i = 0; i < wheelParticles.GetChild(1).childCount; i++)
        {
            primaryParticles.Add(wheelParticles.GetChild(1).GetChild(i).GetComponent<ParticleSystem>());
        }

        foreach (ParticleSystem p in flashParticles.GetComponentsInChildren<ParticleSystem>())
        {
            secondaryParticles.Add(p);
        }
    }

    void Update()
    {
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        // Time.timeScaleをスムーズに変更
        if (Input.GetKeyDown(KeyCode.Space))
        {
            float targetTimeScale = Time.timeScale == 1 ? 0.2f : 1f;
            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, targetTimeScale, 0.2f)
                   .SetEase(Ease.InOutSine);
        }

        // カートの位置をスムーズに追従
        transform.position = sphere.transform.position - new Vector3(0, 0.4f, 0);

        // 前進・後進の動作
        if (verticalInput > 0)
        {
            speed = acceleration;
        }
        else if (verticalInput < 0)
        {
            speed = -acceleration / 2;
        }
        else
        {
            speed = 0;
        }

        // ステアリング制御
        if (horizontalInput != 0)
        {
            int dir = horizontalInput > 0 ? 1 : -1;
            float amount = Mathf.Abs(horizontalInput);
            Steer(dir, amount);
        }

        HandleDrift(horizontalInput);

        currentSpeed = Mathf.SmoothStep(currentSpeed, speed, Time.deltaTime * 12f);
        speed = 0f;
        currentRotate = Mathf.Lerp(currentRotate, rotate, Time.deltaTime * 4f);
        rotate = 0f;

        UpdateAnimations(horizontalInput);
    }

    void HandleDrift(float horizontalInput)
    {
        if (Input.GetButtonDown("Jump") && !drifting && horizontalInput != 0)
        {
            drifting = true;
            driftDirection = horizontalInput > 0 ? 1 : -1;

            foreach (ParticleSystem p in primaryParticles)
            {
                if (!p.isPlaying)
                {
                    p.startColor = Color.clear;
                    p.Play();
                }
            }

            kartModel.parent.DOComplete();
            kartModel.parent.DOPunchPosition(transform.up * .2f, .3f, 5, 1);
        }

        if (drifting)
        {
            float control = (driftDirection == 1)
                ? ExtensionMethods.Remap(horizontalInput, -1, 1, 0, 2)
                : ExtensionMethods.Remap(horizontalInput, -1, 1, 2, 0);
            float powerControl = (driftDirection == 1)
                ? ExtensionMethods.Remap(horizontalInput, -1, 1, .2f, 1)
                : ExtensionMethods.Remap(horizontalInput, -1, 1, 1, .2f);

            Steer(driftDirection, control);
            driftPower += powerControl;

            ColorDrift();
        }

        if (Input.GetButtonUp("Jump") && drifting)
        {
            Boost();
        }
    }

    void UpdateAnimations(float horizontalInput)
    {
        if (!drifting)
        {
            kartModel.localEulerAngles = Vector3.Lerp(
                kartModel.localEulerAngles,
                new Vector3(0, 90 + (horizontalInput * 15), kartModel.localEulerAngles.z),
                .2f
            );
        }
        else
        {
            float control = (driftDirection == 1)
                ? ExtensionMethods.Remap(horizontalInput, -1, 1, .5f, 2)
                : ExtensionMethods.Remap(horizontalInput, -1, 1, 2, .5f);
            kartModel.parent.localRotation = Quaternion.Euler(
                0,
                Mathf.LerpAngle(kartModel.parent.localEulerAngles.y, (control * 15) * driftDirection, .2f),
                0
            );
        }

        frontWheels.localEulerAngles = new Vector3(0, (horizontalInput * 15), frontWheels.localEulerAngles.z);
        frontWheels.localEulerAngles += new Vector3(0, 0, sphere.velocity.magnitude / 2);
        backWheels.localEulerAngles += new Vector3(0, 0, sphere.velocity.magnitude / 2);

        steeringWheel.localEulerAngles = new Vector3(-25, 90, ((horizontalInput * 45)));
    }

    private void FixedUpdate()
    {
        Vector3 accelerationDirection = drifting ? transform.forward : -kartModel.transform.right;
        sphere.AddForce(accelerationDirection * currentSpeed, ForceMode.Acceleration);

        sphere.AddForce(Vector3.down * gravity, ForceMode.Acceleration);

        transform.eulerAngles = Vector3.Lerp(
            transform.eulerAngles,
            new Vector3(0, transform.eulerAngles.y + currentRotate, 0),
            Time.deltaTime * 5f
        );

        RaycastHit hitOn, hitNear;

        if (Physics.Raycast(transform.position + (transform.up * .1f), Vector3.down, out hitOn, 1.1f, layerMask) &&
            Physics.Raycast(transform.position + (transform.up * .1f), Vector3.down, out hitNear, 2.0f, layerMask))
        {
            kartNormal.up = Vector3.Lerp(kartNormal.up, hitNear.normal, Time.deltaTime * 8.0f);
            kartNormal.Rotate(0, transform.eulerAngles.y, 0);
        }
    }

    public void Boost()
    {
        drifting = false;

        if (driftMode > 0)
        {
            DOVirtual.Float(currentSpeed * 3, currentSpeed, .3f * driftMode, Speed);
            DOVirtual.Float(0, 1, .5f, ChromaticAmount).OnComplete(() => DOVirtual.Float(1, 0, .5f, ChromaticAmount));
            kartModel.Find("Tube001").GetComponentInChildren<ParticleSystem>().Play();
            kartModel.Find("Tube002").GetComponentInChildren<ParticleSystem>().Play();
        }

        driftPower = 0;
        driftMode = 0;
        first = false;
        second = false;
        third = false;

        foreach (ParticleSystem p in primaryParticles)
        {
            p.startColor = Color.clear;
            p.Stop();
        }

        kartModel.parent.DOLocalRotate(Vector3.zero, .5f).SetEase(Ease.OutBack);
    }

    public void Steer(int direction, float amount)
    {
        rotate = (steering * direction) * amount;
    }

    public void ColorDrift()
    {
        if (!first) c = Color.clear;

        if (driftPower > 50 && driftPower < 100 - 1 && !first)
        {
            first = true;
            c = turboColors[0];
            driftMode = 1;

            PlayFlashParticle(c);
        }

        if (driftPower > 100 && driftPower < 150 - 1 && !second)
        {
            second = true;
            c = turboColors[1];
            driftMode = 2;

            PlayFlashParticle(c);
        }

        if (driftPower > 150 && !third)
        {
            third = true;
            c = turboColors[2];
            driftMode = 3;

            PlayFlashParticle(c);
        }

        foreach (ParticleSystem p in primaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
        }

        foreach (ParticleSystem p in secondaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
        }
    }

    void PlayFlashParticle(Color c)
    {
        GameObject.Find("CM vcam1").GetComponent<CinemachineImpulseSource>().GenerateImpulse();

        foreach (ParticleSystem p in secondaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
            p.Play();
        }
    }

    private void Speed(float x)
    {
        currentSpeed = x;
    }

    void ChromaticAmount(float x)
    {
        if (Mathf.Abs(postProfile.GetSetting<ChromaticAberration>().intensity.value - x) > 0.01f)
        {
            postProfile.GetSetting<ChromaticAberration>().intensity.value = x;
        }
    }
}
