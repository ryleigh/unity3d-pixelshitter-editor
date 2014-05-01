using UnityEngine;
using System.Collections;

public enum Direction { None, Right, Left, Up, Down };

public class Globals : MonoBehaviour
{
	private static Globals _instance;
	public static Globals shared { get{ return _instance; } }

	public bool DEBUG_MESSAGES = false;
	public bool DEBUG_HITBOXES = false;

	void Awake()
	{
		_instance = this;
	}

	void Start()
	{
	
	}

	public static Direction GetOppositeDirection(Direction direction)
	{
		Direction oppositeDirection = Direction.None;

		switch(direction)
		{
		case Direction.Left:
			oppositeDirection = Direction.Right;
			break;
		case Direction.Right:
			oppositeDirection = Direction.Left;
			break;
		case Direction.Up:
			oppositeDirection = Direction.Down;
			break;
		case Direction.Down:
			oppositeDirection = Direction.Up;
			break;
		}

		return oppositeDirection;
	}
}



















