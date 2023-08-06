program designed to draw and update winter severity bias for CK3.  To that extend it does two things.
1. Read input map and province properties files to generate an image. 
2. Read an edited version of the generated image to create winter severity bias in province properties.
   
Use:

	1. Generating winter severity image:
		Replace the following files/folders in the _Input folder with the ones your game/mod(s) has access to: default.map, definition.csv, provinces.png from map_data. winter severity file(s) from common/province_terrain folder.
		Run _RunMe.bat, this will take about a minute to create all the maps (depending on size of map).
		winter.png will be stored in _Output folder.
  
	Editing winter.png:
		winter severity bias of 0.0 - 1.0 are mapped to Red values of 25 - 225.  with values below 25 and above 255 being moved back to them.
  
	2. Generating winter severity bias text file:
		Make sure you include the map_data files from part 1
		Set averageWinterValues in settings.cfg to true/false depending on preference (option description in settings.cfg)
		Place edited winter.png file into _Input folder (beside setting.cfg)
		Run _RunMe.bat, this will take a few minutes to create all the maps (depending on size of map).
		winter severity bias text file and updated winter.png will be stored in _Output folder.
