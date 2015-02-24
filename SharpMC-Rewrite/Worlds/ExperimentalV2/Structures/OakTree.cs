﻿using SharpMCRewrite.Blocks;

namespace SharpMCRewrite.Worlds.ExperimentalV2.Structures
{
	internal class OakTree : Structure
	{
		public override string Name
		{
			get { return "OakTree"; }
		}

		public override Block[] Blocks
		{
			get
			{
				return new[]
				{
					new Block(17) {Coordinates = new IntVector3(0, 0, 0)},
					new Block(17) {Coordinates = new IntVector3(0, 1, 0)},
					new Block(17) {Coordinates = new IntVector3(0, 2, 0)},
					new Block(17) {Coordinates = new IntVector3(0, 3, 0)},
					new Block(17) {Coordinates = new IntVector3(0, 4, 0)},
					new Block(17) {Coordinates = new IntVector3(0, 5, 0)},
					new Block(18) {Coordinates = new IntVector3(-1, 4, 1)},
					new Block(18) {Coordinates = new IntVector3(1, 4, -1)},
					new Block(18) {Coordinates = new IntVector3(-1, 4, -1)},
					new Block(18) {Coordinates = new IntVector3(1, 4, 1)},
					new Block(18) {Coordinates = new IntVector3(-1, 4, 0)},
					new Block(18) {Coordinates = new IntVector3(1, 4, 0)},
					new Block(18) {Coordinates = new IntVector3(0, 4, -1)},
					new Block(18) {Coordinates = new IntVector3(0, 4, 1)},
					new Block(18) {Coordinates = new IntVector3(-1, 5, 0)},
					new Block(18) {Coordinates = new IntVector3(1, 5, 0)},
					new Block(18) {Coordinates = new IntVector3(0, 5, -1)},
					new Block(18) {Coordinates = new IntVector3(0, 5, 1)},
					new Block(18) {Coordinates = new IntVector3(0, 6, 0)}
				};
			}
		}
	}
}