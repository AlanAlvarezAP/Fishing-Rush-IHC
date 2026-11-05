using System.Collections.Generic;
using UnityEngine;

public class VerletLine : MonoBehaviour
{
    public Transform StartPoint;
    public Transform EndPoint;
    public int Segments = 12;
    public LineRenderer lineRenderer;

    [Header("Configuracion de Cuerda")]
    public float minTotalLength = 0.5f;
    public float currentTotalLength = 0.5f;
    public float targetTotalLength = 0.5f;
    public float maxTotalLength = 50.0f;
    public Vector3 Gravity = new Vector3(0, -9.81f, 0);

    [Header("Fisicas Verlet")]
    public int Iterations = 8;
    public float LerpSpeed = 12.0f;

    [Header("Lomite de Agua")]
    public float waterSurfaceY = -2.0f;
    public bool enableWaterSurface = true;

    private class LineParticle
    {
        public Vector3 Pos;
        public Vector3 OldPos;
        public Vector3 Acceleration;
    }

    private List<LineParticle> particles;

    void Start()
    {
        currentTotalLength = minTotalLength;
        targetTotalLength = minTotalLength;
        InitParticles();
    }

    public void InitParticles()
    {
        if (StartPoint == null || EndPoint == null) return;

        particles = new List<LineParticle>();

        // Evita distancia cero exacta para no romper el solver
        Vector3 start = StartPoint.position;
        Vector3 end = EndPoint.position;
        if (Vector3.Distance(start, end) < 0.01f)
        {
            end += StartPoint.forward * 0.1f;
        }

        for (int i = 0; i < Segments; i++)
        {
            Vector3 point = Vector3.Lerp(start, end, i / (float)(Segments - 1));
            particles.Add(new LineParticle { Pos = point, OldPos = point, Acceleration = Gravity });
        }

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = particles.Count;
            lineRenderer.useWorldSpace = true; // Asegura espacio global
        }
    }

    void Update()
    {
        // Interpola longitud objetivo
        currentTotalLength = Mathf.Lerp(currentTotalLength, targetTotalLength, LerpSpeed * Time.deltaTime);

        // PREVENCI�N DE EXPLOSI�N: La cuerda jam�s puede ser m�s corta que la distancia real entre extremos
        if (StartPoint != null && EndPoint != null)
        {
            float realDistance = Vector3.Distance(StartPoint.position, EndPoint.position);
            if (currentTotalLength < realDistance)
            {
                currentTotalLength = realDistance;
            }
        }
    }

    void FixedUpdate()
    {
        if (StartPoint == null || EndPoint == null || particles == null || particles.Count == 0) return;

        float dt = Time.fixedDeltaTime;
        foreach (var p in particles)
        {
            Verlet(p, dt);
        }

        // Calcula la distancia requerida por cada segmento
        float segLength = currentTotalLength / Mathf.Max(1, Segments - 1);

        for (int i = 0; i < Iterations; i++)
        {
            for (int j = 0; j < particles.Count - 1; j++)
            {
                PoleConstraint(particles[j], particles[j + 1], segLength);
            }
        }

        // --- RESTRICCIÓN PARA FLOTACIÓN DE CUERDA ---
        if (enableWaterSurface && particles != null)
        {
            foreach (var p in particles)
            {
                if (p.Pos.y < waterSurfaceY)
                {
                    p.Pos.y = waterSurfaceY;
                }
            }
        }

        // Fijar extremos exactamente en la ca�a y el anzuelo
        particles[0].Pos = StartPoint.position;
        particles[particles.Count - 1].Pos = EndPoint.position;
    }

    // Renderizado en LateUpdate para sincronizar perfectamente con animaciones y transform
    void LateUpdate()
    {
        if (lineRenderer == null || particles == null || particles.Count == 0) return;
        if (StartPoint == null || EndPoint == null) return;

        // Asegurar que los extremos est�n pegados en el render de este frame
        particles[0].Pos = StartPoint.position;
        particles[particles.Count - 1].Pos = EndPoint.position;

        Vector3[] positions = new Vector3[particles.Count];
        for (int i = 0; i < particles.Count; i++)
        {
            positions[i] = particles[i].Pos;
        }
        lineRenderer.SetPositions(positions);
    }

    public void SetTargetLength(float length)
    {
        targetTotalLength = Mathf.Clamp(length, minTotalLength, maxTotalLength);
    }

    public void SnapToLength(float length)
    {
        targetTotalLength = Mathf.Clamp(length, minTotalLength, maxTotalLength);
        currentTotalLength = targetTotalLength;
    }

    public void ResetRope()
    {
        targetTotalLength = minTotalLength;
        currentTotalLength = minTotalLength;
        InitParticles();
    }

    private void Verlet(LineParticle p, float dt)
    {
        var temp = p.Pos;
        p.Pos += (p.Pos - p.OldPos) + (p.Acceleration * dt * dt);
        p.OldPos = temp;
    }

    private void PoleConstraint(LineParticle p1, LineParticle p2, float restLength)
    {
        var delta = p2.Pos - p1.Pos;
        var deltaLength = delta.magnitude;

        if (deltaLength < 0.0001f) return;

        var diff = (deltaLength - restLength) / deltaLength;
        p1.Pos += delta * diff * 0.5f;
        p2.Pos -= delta * diff * 0.5f;
    }
}
