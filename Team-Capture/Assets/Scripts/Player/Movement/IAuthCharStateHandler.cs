namespace Player.Movement
{
	//This code is built on unity-fastpacedmultiplayer
	//https://github.com/JoaoBorks/unity-fastpacedmultiplayer
	//
	//MIT License
	//Copyright (c) 2015 ultimatematchthree, 2017 Joao Borks [joao.borks@gmail.com]

	/// <summary>
	/// Handles player's state
	/// </summary>
	public interface IAuthCharStateHandler
	{
		void OnStateChange(CharacterState newState);
	}
}