using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Star_Collectible : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(Vector3.up * 100 * Time.deltaTime);
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Player>())
            {
                _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.CollectStars, 1);
                Destroy(gameObject);
            }
        }
    }
}
