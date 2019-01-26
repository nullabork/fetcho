/*
 * Created by SharpDevelop.
 * User: Alistair
 * Date: 6/01/2019
 * Time: 10:30 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
namespace Fetcho
{
	public interface ILinkExtractor
	{
	  Uri CurrentSourceUri { get; set; }
		Uri NextUri();
	}
}


