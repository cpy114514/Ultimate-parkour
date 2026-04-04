using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandBeaconGroup : MonoBehaviour
{
    public int requiredLitCount = 2;
    public float completionScore = 3f;
    public float completionDelay = 0.6f;
    public List<SandBeacon> beacons = new List<SandBeacon>();

    bool completed;
    Coroutine completionRoutine;

    void OnValidate()
    {
        requiredLitCount = Mathf.Max(1, requiredLitCount);
        completionScore = Mathf.Max(0f, completionScore);
        completionDelay = Mathf.Max(0f, completionDelay);
        beacons.RemoveAll(item => item == null);
    }

    public void NotifyBeaconActivated(SandBeacon source, PlayerController.ControlType activator)
    {
        if (completed)
        {
            return;
        }

        List<SandBeacon> groupBeacons = GetTrackedBeacons();
        List<SandBeacon> activatedBeacons = new List<SandBeacon>();

        for (int i = 0; i < groupBeacons.Count; i++)
        {
            SandBeacon beacon = groupBeacons[i];
            if (beacon != null && beacon.IsActivated)
            {
                activatedBeacons.Add(beacon);
            }
        }

        if (activatedBeacons.Count < requiredLitCount)
        {
            return;
        }

        completed = true;
        for (int i = 0; i < activatedBeacons.Count; i++)
        {
            activatedBeacons[i].SetBeamActive(true);
        }

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
        }

        completionRoutine = StartCoroutine(CompleteObjectiveAfterDelay(activator));
    }

    public void NotifyBeaconLit(SandBeacon source, PlayerController.ControlType activator)
    {
        NotifyBeaconActivated(source, activator);
    }

    public void ResetGroup()
    {
        completed = false;

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        List<SandBeacon> groupBeacons = GetTrackedBeacons();
        for (int i = 0; i < groupBeacons.Count; i++)
        {
            if (groupBeacons[i] != null)
            {
                groupBeacons[i].SetBeamActive(false);
            }
        }
    }

    IEnumerator CompleteObjectiveAfterDelay(PlayerController.ControlType activator)
    {
        if (completionDelay > 0f)
        {
            yield return new WaitForSeconds(completionDelay);
        }

        RoundManager.Instance?.TriggerObjectiveRoundWin(activator, completionScore);
        completionRoutine = null;
    }

    List<SandBeacon> GetTrackedBeacons()
    {
        beacons.RemoveAll(item => item == null);

        SandBeacon[] sceneBeacons = FindObjectsOfType<SandBeacon>(true);
        for (int i = 0; i < sceneBeacons.Length; i++)
        {
            SandBeacon beacon = sceneBeacons[i];
            if (beacon != null && beacon.group == this && !beacons.Contains(beacon))
            {
                beacons.Add(beacon);
            }
        }

        return beacons;
    }
}
