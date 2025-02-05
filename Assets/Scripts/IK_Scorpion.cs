﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OctopusController;
using System.Linq;
using System.Drawing;
using Color = UnityEngine.Color;

public class IK_Scorpion : MonoBehaviour
{
    [Header("Body")]
    public float animTime;
    public float animDuration = 5;
    bool animPlaying = false;
    public Transform Body;
    public Transform StartPos;
    public Transform EndPos;

    [Header("Tail")]
    public Transform tailTarget;
    public Transform tail;
    private Transform[] _tailBones;
    private Quaternion[] _originalTaillRotations;

    [Header("Legs")]
    public Transform[] legs;
    public Transform[] legTargets;
    public Transform[] futureLegBases;
    public Transform[] legRayCast;

    public Slider forceSlider;
    private bool _sliderGoUp=true;
    public float _slideSpeed = 20f;

    public Slider magnusSlider;
    private Vector3 _targetWithMagnus;
    public bool firstTimeMagnus = true;
    private float _map;

    /***************************************Iks**************************************/
    //TAIL
    public Transform _tailTarget;
    MyTentacleController _tail;
    float[] _tailAngles = null;
    Vector3[] _tailAxis = null;
    Vector3[] _tailOffset = null;
    private float _deltaGradient = 0.1f; // Used to simulate gradient (degrees)
    private float _learningRate = 3.0f; // How much we move depending on the gradient
    private float _distanceThreshold = 2.0f;
    private Transform _endEffector;

    //LEGS
    Transform[] _legTargets = null;
    Transform[] _legRoots = null;
    Transform[] _legFutureBases = null;
    Transform[] _raycastPos = null;

    Vector3[] _finalPosY = null;
    MyTentacleController[] _legs = new MyTentacleController[6];
    bool _startWalk;
    private List<Vector3[]> _copy;
    private List<float[]> _distances;
    public float _legThreshold;
    public float lerpDuration;
    private float elapsedTime, elapsedTime2;
    float[] complete = null;
    private bool[] limit;
    private Vector3[] initialPos = null;
    private Vector3[] finalPos = null,finalPosY;
    private Transform copy2;

    [SerializeField]
    private float _distanceWeight;

    [SerializeField]
    private float _angleWeight;

    public float height;
    private Ray[] _rayDown;
    private RaycastHit[] _hitDown;
    private float _y_promedio, _y_total;
    private Transform _posicionInicialBody;
    private Vector3 _posicionDeseadaBody;

    // Start is called before the first frame update
    void Start()
    {
        _originalTaillRotations = new Quaternion[5];
        _tailBones = new Transform[5];
        _tailBones[0] = tail;
        _tailBones[1] = _tailBones[0].GetChild(1);
        _tailBones[2] = _tailBones[1].GetChild(1);
        _tailBones[3] = _tailBones[2].GetChild(1);
        _tailBones[4] = _tailBones[3].GetChild(1);
        _endEffector = _tailBones[4];
        for (int i = 0; i < _tailBones.Length; i++) _originalTaillRotations[i] = _tailBones[i].rotation;

        InitLegs(legs, futureLegBases, legTargets, legRayCast);
        InitTail(tail);
        _rayDown = new Ray[legs.Length];
        _hitDown = new RaycastHit[legs.Length];
        limit = new bool[legs.Length];
        _y_promedio = 0;
    }

    // Update is called once per frame
    void Update()
    {
        _y_total = 0;
        for (int i = 0; i < legs.Length; i++)
        {
            _y_total += _legFutureBases[i].position.y;
            _rayDown[i] = new Ray(legRayCast[i].transform.position, -Vector3.up);
            if (Physics.Raycast(_rayDown[i], out _hitDown[i]))
            {
                if (_hitDown[i].collider.tag == "Suelo")
                {
                    if (_hitDown[i].distance > 0.01)
                    {
                        futureLegBases[i].transform.position = new Vector3(futureLegBases[i].transform.position.x, _hitDown[i].point.y, futureLegBases[i].transform.position.z);
                    }
                }
                if (_hitDown[i].collider.tag == "Obstacle")
                {
                    if (_hitDown[0].distance > 0.01 || _hitDown[2].distance > 0.01 || _hitDown[4].distance > 0.01)
                    {
                        futureLegBases[i].transform.position = new Vector3(futureLegBases[i].transform.position.x, _hitDown[i].point.y, futureLegBases[i].transform.position.z);
                        /*
                         AQUI INTENTE LA ROTACION, PERO NO ME HA FUNCIONADO
                        Vector3 diferenciaIzq = futureLegBases[0].position - futureLegBases[2].position;
                        Vector3 diferenciaDer = futureLegBases[1].position - futureLegBases[3].position;
                        Vector3 diferenciaTrasera = futureLegBases[4].position - futureLegBases[5].position;
                        Vector3 Cross = Vector3.Cross(diferenciaDer, diferenciaTrasera).normalized;
                        Vector3 Cross2 = Vector3.Cross(diferenciaIzq, Cross).normalized;

                        Body.transform.Rotate(Cross2*0.1);
                        */
                    }
                    if (_hitDown[1].distance > 0.01 || _hitDown[3].distance > 0.01 || _hitDown[5].distance > 0.01)
                    {
                        futureLegBases[i].transform.position = new Vector3(futureLegBases[i].transform.position.x, _hitDown[i].point.y, futureLegBases[i].transform.position.z);

                    }
                }
            }
        }
        //REALIZO EL PROMEDIO DE LA POSICION Y DE LAS PATAS, PARA APLICARSELO AL CUERPO Y CAMBIE SU POSICION EN EJE Y,
        //DEPENDIENDO DE LA ALTURA DE LAS PATAS
        _y_promedio = _y_total / legs.Length;
        _y_promedio = _y_promedio + height;
       _posicionInicialBody = Body;
       _posicionDeseadaBody = new Vector3(Body.position.x, _y_promedio, Body.position.z);

        

        if (animPlaying)
            animTime += Time.deltaTime;

        NotifyTailTarget();

        if (Input.GetKey(KeyCode.Space))
        {
            if (forceSlider.value >= forceSlider.maxValue) _sliderGoUp = false;
            if (forceSlider.value <= forceSlider.minValue) _sliderGoUp = true;

            if (_sliderGoUp) forceSlider.value += Time.deltaTime * _slideSpeed;
            else forceSlider.value -= Time.deltaTime * _slideSpeed;
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            _tailTarget = null;
            for (int i = 0; i < _tailBones.Length; i++) _tailBones[i].rotation = _originalTaillRotations[i];
            _learningRate = forceSlider.value;
            InitTail(tail);
            StartWalk();
        }

        if (animTime < animDuration)
        {
            
            Body.position = Vector3.Lerp(StartPos.position, EndPos.position, animTime / animDuration);
            //APLICO LERP DEL PROMEDIO DE LA ALTURA DE LAS PATAS AL CUERPO
            Body.position = Vector3.Lerp(_posicionInicialBody.position, _posicionDeseadaBody, lerpDuration);
        }
        else if (animTime >= animDuration && animPlaying)
        {
            Body.position = EndPos.position;
            animPlaying = false;
        }

        UpdateIK();
    }

    //Function to send the tail target transform to the dll
    public void NotifyTailTarget()
    {
        NotifyTailTargetIK(tailTarget);
    }

    //Trigger Function to start the walk animation
    public void NotifyStartWalk()
    {
        NotifyStartWalkIK();
    }

    public void StartWalk()
    {
        NotifyStartWalk();
        animTime = 0;
        animPlaying = true;
    }

    /*****************************IKs**********************************/
    public void InitLegs(Transform[] LegRoots, Transform[] LegFutureBases, Transform[] LegTargets, Transform[] LegRayCast)
    {
        _legs = new MyTentacleController[LegRoots.Length];
        _legRoots = new Transform[LegRoots.Length];
        _legTargets = new Transform[LegTargets.Length];
        _legFutureBases = new Transform[LegFutureBases.Length];
        _raycastPos = new Transform[legs.Length];
        _copy = new List<Vector3[]>();
        _distances = new List<float[]>();
        _finalPosY = new Vector3[LegRoots.Length];
        complete = new float[LegRoots.Length];
        elapsedTime = 0;
        initialPos = new Vector3[LegRoots.Length];
        finalPos = new Vector3[LegRoots.Length];
        finalPosY = new Vector3[LegRoots.Length];

        //Legs init
        for (int i = 0; i < LegRoots.Length; i++)
        {
            _legs[i] = new MyTentacleController();
            _legs[i].LoadTentacleJoints(LegRoots[i], TentacleMode.LEG);
            _legRoots[i] = LegRoots[i];
            _legTargets[i] = LegTargets[i];
            _legFutureBases[i] = LegFutureBases[i];
            _raycastPos[i] = LegRayCast[i];
            _copy.Add(new Vector3[_legs[i].Bones.Length]);
            _distances.Add(new float[_legs[i].Bones.Length]);
            initialPos[i] = _legRoots[i].position;
            finalPos[i] = _legFutureBases[i].position;
            _finalPosY[i] = new Vector3(_legFutureBases[i].position.x, _legFutureBases[i].position.y + 0.5f, _legFutureBases[i].position.z);
            for (int x = 0; x < _legs[i].Bones.Length; x++)
            {
                if (x < _legs[i].Bones.Length - 1)
                {
                    _distances[i][x] = (_legs[i].Bones[x + 1].position - _legs[i].Bones[x].position).magnitude;
                }
                else
                {
                    _distances[i][x] = 0f;
                }
            }
        }
    }

    public void InitTail(Transform TailBase)
    {
        _tail = new MyTentacleController();
        _tail.LoadTentacleJoints(TailBase, TentacleMode.TAIL);
        _tailAngles = new float[_tail.Bones.Length];
        _tailAxis = new Vector3[_tail.Bones.Length];
        _tailOffset = new Vector3[_tail.Bones.Length];

        _tailAxis[0] = new Vector3(0, 0, 1);
        _tailAxis[1] = new Vector3(1, 0, 0);
        _tailAxis[2] = new Vector3(1, 0, 0);
        _tailAxis[3] = new Vector3(1, 0, 0);
        _tailAxis[4] = new Vector3(1, 0, 0);
        _tailAxis[5] = new Vector3(1, 0, 0);

        for (int i = 0; i < _tailOffset.Length; i++)
        {
            if (i != 0) _tailOffset[i] = Quaternion.Inverse(_tail.Bones[i - 1].rotation) * (_tail.Bones[i].position - _tail.Bones[i - 1].position);
            else _tailOffset[i] = _tail.Bones[i].position;
        }
    }

    //Check when to start the animation towards target and implement Gradient Descent method to move the joints.
    public void NotifyTailTargetIK(Transform target)
    {
        if (Vector3.Distance(_tail.Bones[_tail.Bones.Length - 1].position, target.position) < _distanceThreshold)
        {
            if (firstTimeMagnus)
            {
                _map = Mathf.Lerp(-0.5f, +0.5f, Mathf.InverseLerp(magnusSlider.minValue, magnusSlider.maxValue, magnusSlider.value));
                firstTimeMagnus = false;
            }

            _tailTarget = target;
        }
    }

    //Notifies the start of the walking animation
    public void NotifyStartWalkIK()
    {
        _startWalk = true;
    }

    //Create the apropiate animations and update the IK from the legs and tail
    public void UpdateIK()
    {
        if (_tailTarget != null) updateTail();
        if (_startWalk)
        {
            updateLegs();
            updateLegPos();
        }
    }

    private void updateLegPos()
    {
        for (int i = 0; i < _legs.Length; i++)
        {
            //LERP DE LAS PATAS
            if ((_legFutureBases[i].position - _legRoots[i].position).magnitude > _legThreshold && limit[i] == false)
            {
                //CADA VEZ QUE SE HACE LA ANIMACION, SE REINICIAN LOS PARAMETROS
                limit[i] = true;
                elapsedTime = 0;
                elapsedTime2 = 0;
                initialPos[i] = _legRoots[i].position;
                finalPos[i] = _legFutureBases[i].position;
                finalPosY[i] = new Vector3(_legFutureBases[i].position.x, _legFutureBases[i].position.y + height, _legFutureBases[i].position.z);

            }
            if (limit[i] == true)
            {
                elapsedTime += Time.deltaTime;
                //LERP EJE Y
                _legRoots[i].position = Vector3.Lerp(initialPos[i], finalPosY[i], elapsedTime / lerpDuration);

                if (elapsedTime >=lerpDuration)
                {
                    elapsedTime2 += Time.deltaTime;
                    //CUANDO TERMINA EL LERP EJE Y, DESDE ESA POSICION LLEGA AL FUTURE_BASE
                    _legRoots[i].position = Vector3.Lerp(finalPosY[i], finalPos[i], elapsedTime2 / lerpDuration);
                    if(elapsedTime2 >= lerpDuration)
                    {
                        limit[i] = false;
                    }
                    
                }
            }
        }
    }
    
    //Implement Gradient Descent method to move tail if necessary
    private void updateTail()
    {
        if (_tailTarget != null)
        {
            _targetWithMagnus = new Vector3(_tailTarget.position.x + _map, _tailTarget.position.y, _tailTarget.position.z);
            for (int i = 0; i < _tail.Bones.Length; i++)
            {
                float gradient = CalculateGradient(_targetWithMagnus, _tailAngles, i, _deltaGradient);
                _tailAngles[i] -= _learningRate * gradient;
            }

            for (int i = 0; i < _tail.Bones.Length; i++)
            {
                if (_tailAxis[i].x == 1) _tail.Bones[i].localEulerAngles = new Vector3(_tailAngles[i], 0, 0);
                else if (_tailAxis[i].y == 1) _tail.Bones[i].localEulerAngles = new Vector3(0, _tailAngles[i], 0);
                else if (_tailAxis[i].z == 1) _tail.Bones[i].localEulerAngles = new Vector3(0, 0, _tailAngles[i]);
            }
        }
    }

    private float CalculateGradient(Vector3 target, float[] Solution, int i, float delta)
    {
        float gradient = 0;
        float angle = Solution[i];
        float p = DistanceFromTarget(target, Solution) * _distanceWeight + AngleDiff() * _angleWeight;
        Solution[i] += delta;
        float pDelta = DistanceFromTarget(target, Solution) * _distanceWeight + AngleDiff() * _angleWeight;
        gradient = (pDelta - p) / delta;
        Solution[i] = angle;
        return gradient;
    }

    private float DistanceFromTarget(Vector3 target, float[] Solution)
    {
        Vector3 point = ForwardKinematics(Solution);
        return Vector3.Distance(point, target);
    }

    private float AngleDiff()
    {
        return Mathf.Abs(Quaternion.Angle(_endEffector.rotation, _tailTarget.rotation)/180);
    }

    public PositionRotation ForwardKinematics(float[] Solution)
    {
        Vector3 prevPoint = _tail.Bones[0].transform.position;

        Quaternion rotation = _tail.Bones[0].transform.parent.rotation;

        for (int i = 1; i < _tail.Bones.Length; i++)
        {
            rotation *= Quaternion.AngleAxis(Solution[i - 1], _tailAxis[i - 1]);
            Vector3 nextPoint = prevPoint + rotation * _tailOffset[i];
            Debug.DrawLine(prevPoint, nextPoint);

            prevPoint = nextPoint;
        }
        // The end of the effector
        return new PositionRotation(prevPoint, rotation);
    }

    //Implement fabrik method to move legs 
    private void updateLegs()
    {
        for (int i = 0; i < _legs.Length; i++)
        {
            fabrik(_legs[i].Bones, _legTargets[i], _distances[i], _copy[i], _legFutureBases[i]);
        }

    }

    public void fabrik(Transform[] joints, Transform target, float[] distances, Vector3[] copy, Transform futurBase)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            copy[i] = joints[i].position;
        }

        float targetRootDist = Vector3.Distance(copy[0], target.position);

        if (targetRootDist > distances.Sum())
        {
            for (int i = 0; i <= joints.Length - 2; i++)
            {
                joints[i].transform.position = copy[i];
            }
        }
        else
        {
            Vector3[] inversePositions = new Vector3[copy.Length];
            for (int i = (copy.Length - 1); i >= 0; i--)
            {
                if (i == copy.Length - 1)
                {
                    copy[i] = target.transform.position;
                    inversePositions[i] = target.transform.position;
                }
                else
                {
                    Vector3 posPrimaSiguiente = inversePositions[i + 1];
                    Vector3 posBaseActual = copy[i];
                    Vector3 direccion = (posBaseActual - posPrimaSiguiente).normalized;
                    float longitud = distances[i];
                    inversePositions[i] = posPrimaSiguiente + (direccion * longitud);
                }
            }
            for (int i = 0; i < inversePositions.Length; i++)
            {
                if (i == 0)
                {
                    copy[i] = joints[0].position;
                }
                else
                {
                    Vector3 posPrimaActual = inversePositions[i];
                    Vector3 posPrimaSegundaAnterior = copy[i - 1];
                    Vector3 direccion = (posPrimaActual - copy[i - 1]).normalized;
                    float longitud = distances[i - 1];
                    copy[i] = posPrimaSegundaAnterior + (direccion * longitud);
                }
            }
        }

        for (int i = 0; i < joints.Length - 1; i++)
        {
            Vector3 joint01 = (joints[i + 1].position - joints[i].position).normalized;
            Vector3 copy01 = (copy[i + 1] - copy[i]).normalized;

            Vector3 crossBones01 = Vector3.Cross(joint01, copy01).normalized;

            float angle01 = Mathf.Acos(Vector3.Dot(joint01, copy01)) * Mathf.Rad2Deg;

            if (angle01 > 1f)
            {
                joints[i].rotation = Quaternion.AngleAxis(angle01, crossBones01) * joints[i].rotation;
            }
        }

    }

}
