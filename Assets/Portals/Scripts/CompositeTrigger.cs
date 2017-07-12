using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CompositeTrigger : MonoBehaviour {

    private Dictionary<Collider, int> _colliderCounts;
    private Dictionary<Rigidbody, int> _rigidbodyCounts;

    void Awake() {
        _colliderCounts = new Dictionary<Collider, int>();
        _rigidbodyCounts = new Dictionary<Rigidbody, int>();
    }

    void OnEnable() {
        // Support live recompile
        if (_colliderCounts == null || _rigidbodyCounts == null) {
            Awake();
        }
    }

    void OnDisable() {
        _colliderCounts.Clear();
        _rigidbodyCounts.Clear();
    }

    void OnTriggerEnter(Collider collider) {
        Rigidbody rigidbody = collider.GetComponentInParent<Rigidbody>();
        if (rigidbody) {
            int count = IncrementCount(_rigidbodyCounts, rigidbody);
            if (count == 1) {
                rigidbody.SendMessage("OnCompositeTriggerEnter", this, SendMessageOptions.DontRequireReceiver);
            }
        } else {
            int count = IncrementCount(_colliderCounts, collider);
            if (count == 1) {
                collider.SendMessage("OnCompositeTriggerEnter", this, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    void OnTriggerStay(Collider collider) {
        Rigidbody rigidbody = collider.GetComponentInParent<Rigidbody>();
        if (rigidbody) {
            rigidbody.SendMessage("OnCompositeTriggerStay", this, SendMessageOptions.DontRequireReceiver);
        } else {
            collider.SendMessage("OnCompositeTriggerStay", this, SendMessageOptions.DontRequireReceiver);
        }
    }

    void OnTriggerExit(Collider collider) {
        Rigidbody rigidbody = collider.GetComponentInParent<Rigidbody>();
        if (rigidbody) {
            int count = DecrementCount(_rigidbodyCounts, rigidbody);
            if (count == 0) {
                rigidbody.SendMessage("OnCompositeTriggerExit", this, SendMessageOptions.DontRequireReceiver);
            }
        } else {
            int count = DecrementCount(_colliderCounts, collider);
            if (count == 1) {
                collider.SendMessage("OnCompositeTriggerEnter", this, SendMessageOptions.DontRequireReceiver);
            }
        }

    }

    int IncrementCount<T>(Dictionary <T, int> dictionary, T t) {
        int count = 0;
        if (!dictionary.TryGetValue(t, out count)) {
            count = 1;
        } else {
            count += 1;
        }
        dictionary[t] = count;
        return count;
    }

    int DecrementCount<T>(Dictionary<T, int> dictionary, T t) {
        int count = 0;
        if (!dictionary.TryGetValue(t, out count)) {
            throw new System.Exception("Attempted to decrement trigger count below zero. This should never happen");
        } else {
            count -= 1;
            if (count == 0) {
                dictionary.Remove(t);
            } else {
                dictionary[t] = count;
            }
        }
        return count;
    }
}
