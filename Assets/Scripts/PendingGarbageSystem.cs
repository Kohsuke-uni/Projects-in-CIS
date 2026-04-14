using System;
using System.Collections.Generic;
using UnityEngine;

public class PendingGarbageSystem : MonoBehaviour
{
    [System.Serializable]
    private class GarbagePacket
    {
        public int packetId;
        public int lines;
        public float remainingDelay;

        public GarbagePacket(int packetId, int lines, float delay)
        {
            this.packetId = packetId;
            this.lines = lines;
            this.remainingDelay = Mathf.Max(0f, delay);
        }

        public bool IsReady()
        {
            return remainingDelay <= 0f;
        }
    }

    [Header("Target Board")]
    public Board targetBoard;

    [Header("Timing")]
    public bool useUnscaledTime = false;

    public event Action<int> OnPendingGarbageChanged;

    private readonly List<GarbagePacket> packets = new List<GarbagePacket>();

    public int PendingLineCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < packets.Count; i++)
                total += packets[i].lines;
            return total;
        }
    }

    public int ReadyPendingLineCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < packets.Count; i++)
            {
                if (packets[i].IsReady())
                    total += packets[i].lines;
            }
            return total;
        }
    }

    private void Update()
    {
        if (packets.Count == 0)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        bool readyStateChanged = false;

        for (int i = 0; i < packets.Count; i++)
        {
            if (packets[i].remainingDelay > 0f)
            {
                float before = packets[i].remainingDelay;
                packets[i].remainingDelay = Mathf.Max(0f, packets[i].remainingDelay - dt);

                if (before > 0f && packets[i].remainingDelay <= 0f)
                {
                    readyStateChanged = true;
                    Debug.Log($"[PendingGarbageSystem] packet ready id={packets[i].packetId}, lines={packets[i].lines}");
                }
            }
        }

        if (readyStateChanged)
            NotifyPendingChanged();
    }

    public void ReceiveGarbagePacket(int packetId, int lines, float delay)
    {
        if (lines <= 0)
            return;

        packets.Add(new GarbagePacket(packetId, lines, delay));
        Debug.Log($"[PendingGarbageSystem] receive packet id={packetId}, lines={lines}, delay={delay}");
        NotifyPendingChanged();
    }

    public int CancelPendingGarbage(int attackAmount)
    {
        if (attackAmount <= 0)
            return 0;

        int remain = attackAmount;

        for (int i = 0; i < packets.Count && remain > 0; i++)
        {
            GarbagePacket packet = packets[i];
            if (packet.lines <= 0)
                continue;

            int cancel = Mathf.Min(packet.lines, remain);
            packet.lines -= cancel;
            remain -= cancel;

            Debug.Log($"[PendingGarbageSystem] cancel packet id={packet.packetId}, canceled={cancel}, left={packet.lines}");
        }

        for (int i = packets.Count - 1; i >= 0; i--)
        {
            if (packets[i].lines <= 0)
                packets.RemoveAt(i);
        }

        NotifyPendingChanged();
        return remain;
    }

    public void ClearPending()
    {
        packets.Clear();
        NotifyPendingChanged();
    }

    public void ApplyPendingGarbage()
    {
        ForceReleaseAllReadyPackets();
    }

    public void ForceReleaseNextReadyPacket()
    {
        if (targetBoard == null)
            return;

        for (int i = 0; i < packets.Count; i++)
        {
            if (!packets[i].IsReady())
                continue;

            GarbagePacket packet = packets[i];
            packets.RemoveAt(i);

            if (packet.lines > 0)
            {
                targetBoard.ApplyGarbageLinesNow(packet.lines);
                Debug.Log($"[PendingGarbageSystem] release one ready packet id={packet.packetId}, lines={packet.lines}");
            }

            NotifyPendingChanged();
            return;
        }
    }

    public void ForceReleaseAllReadyPackets()
    {
        ReleaseReadyPackets();
    }

    private void ReleaseReadyPackets()
    {
        if (targetBoard == null)
            return;

        bool releasedAny = false;

        while (packets.Count > 0 && packets[0].IsReady())
        {
            GarbagePacket packet = packets[0];
            packets.RemoveAt(0);

            if (packet.lines > 0)
            {
                targetBoard.ApplyGarbageLinesNow(packet.lines);
                Debug.Log($"[PendingGarbageSystem] release ready packet id={packet.packetId}, lines={packet.lines}");
                releasedAny = true;
            }
        }

        if (releasedAny)
            NotifyPendingChanged();
    }

    private void NotifyPendingChanged()
    {
        OnPendingGarbageChanged?.Invoke(PendingLineCount);
    }
}
