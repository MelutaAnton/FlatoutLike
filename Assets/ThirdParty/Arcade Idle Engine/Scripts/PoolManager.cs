using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
	[System.Serializable]
	public class ItemPool
    {
		public GameObject ItemPref;
		public List<GameObject> ItemPools = new List<GameObject>();
	}
	public class PoolManager : MonoBehaviour
	{
		public int PoolSize = 20;

		public GameObject CashSpendPref;
		GameObject[] CashSpendpools;
		public GameObject NpcPref;
		public GameObject CashPref;
		public GameObject NpcCarPref;

		[Space(5)]
		public List<ItemPool> itemPools = new List<ItemPool>();
		public List<GameObject> NpcPool = new List<GameObject>();
		public List<GameObject> NpcCarPool = new List<GameObject>();
		public List<GameObject> CashPools = new List<GameObject>();
		public List<GameObject> BoxPools = new List<GameObject>();

		// Start is called before the first frame update
		void Start()
		{

			CashSpendpools = new GameObject[PoolSize];
			for (int i = 0; i < CashSpendpools.Length; i++)
			{
				GameObject Go = Instantiate(CashSpendPref, transform);
				Go.SetActive(false);
				CashSpendpools[i] = Go;
			}
		}
		
		public GameObject GetItem(Transform Parent,GameObject ItemPref)
		{
			bool found = false;
			int WhichPool = 0;
			for(int i = 0; i < itemPools.Count; i++)
            {
                if (itemPools[i].ItemPref == ItemPref)
                {
					WhichPool = i;
					found = true;
					break;
                }
            }
            if (!found)
            {
				ItemPool thepool=new ItemPool();
				thepool.ItemPref = ItemPref;
				itemPools.Add(thepool);
				WhichPool = itemPools.Count-1;
            }

			for (int i = 0; i < itemPools[WhichPool].ItemPools.Count; i++)
			{
				if (itemPools[WhichPool].ItemPools[i] && !itemPools[WhichPool].ItemPools[i].activeSelf)
				{
					itemPools[WhichPool].ItemPools[i].transform.SetParent(Parent);
					if (ItemPref.GetComponent<TheItem>())
					{
						itemPools[WhichPool].ItemPools[i].GetComponent<TheItem>().SetStep(Step.Default);
					}
					itemPools[WhichPool].ItemPools[i].transform.localScale = Vector3.one;
                    if (itemPools[WhichPool].ItemPools[i].GetComponent<Crate>())
                    {
						itemPools[WhichPool].ItemPools[i].GetComponent<Crate>().Deactivate();

					}
					return itemPools[WhichPool].ItemPools[i];

				}
			}
			GameObject Go = Instantiate(ItemPref, Parent);

			itemPools[WhichPool].ItemPools.Add(Go);

			return Go;
		}
		public GameObject GetCash(Transform Parent)
		{
			for (int i = 0; i < CashPools.Count; i++)
			{
				if (CashPools[i] && !CashPools[i].activeSelf)
				{
					//CashPools[i].SetActive(true);
					CashPools[i].transform.SetParent(Parent);
					CashPools[i].transform.localScale = Vector3.one;
					return CashPools[i];

				}
			}
			GameObject Go = Instantiate(CashPref, Parent);
			CashPools.Add(Go);
			return Go;
		}
		public GameObject GetNpc(Transform TheParent)
		{
			for (int i = 0; i < NpcPool.Count; i++)
			{
				if (NpcPool[i] && !NpcPool[i].activeSelf)
				{
					NpcPool[i].transform.SetParent(TheParent);
					NpcPool[i].SetActive(true);
					return NpcPool[i];

				}
			}
			GameObject Go = Instantiate(NpcPref, TheParent);
			NpcPool.Add(Go);
			return Go;
		}
		public GameObject GetNpcCar(Transform TheParent)
		{
			for (int i = 0; i < NpcCarPool.Count; i++)
			{
				if (NpcCarPool[i] && !NpcCarPool[i].activeSelf)
				{
					NpcCarPool[i].SetActive(true);
					return NpcCarPool[i];

				}
			}
			GameObject Go = Instantiate(NpcCarPref, TheParent);
			NpcCarPool.Add(Go);
			return Go;
		}
		public GameObject GetCashSpendObj()
		{
			for (int i = 0; i < CashSpendpools.Length; i++)
			{
				if (!CashSpendpools[i].activeSelf)
				{
					return CashSpendpools[i];
				}
			}
			return null;
		}

		public void _MoveObj(Transform Which, Transform[] targets, float duration, Vector3 Finaltarget, float VectorArc, float MinDist)
		{
			StartCoroutine(MoveObj(Which, targets, duration, Finaltarget, VectorArc, MinDist));
		}
		IEnumerator MoveObj(Transform Which, Transform[] targets, float duration, Vector3 Finaltarget, float VectorArc, float MinDist)
		{
			bool IsDone = false;
			int activetarget = 0;
			float T = 0;
			while (!IsDone)
			{
				Transform t = targets[activetarget];
				T += Time.deltaTime;
				float t01 = T / duration;
				Vector3 Pos_A = Which.position;
				Vector3 Pos_B = Finaltarget;
				Vector3 pos = Vector3.MoveTowards(Pos_A, Pos_B, t01);

				Vector3 Arc = Mathf.Sin(t01 * 3.14f) * VectorArc * Vector3.up;
				Which.position = pos + Arc;
				float Dist = Vector3.Distance(Which.position, Pos_B);
				if (Dist < MinDist || T > duration)
				{
					IsDone = true;
				}


				if (activetarget < targets.Length - 1)
				{
					Which.LookAt(targets[activetarget]);

				}
				//Debug.Log(Which.position);
				yield return new WaitForSeconds(0.000001f);
			}
			Which.gameObject.SetActive(false);
		}
	}
}