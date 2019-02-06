
@echo off
mkdir data
set datapath=G:\share\fetchodata
set datapath=data
set current=0
set previous=0

fetcho %datapath%\nextlinks-%previous%.txt %datapath%\requeue-%current%.txt > %datapath%\packet-%current%.xml
echo %datapath%\packet-%current%.xml

:loop

  extracto %datapath%\packet-%current%.xml > %datapath%\links-%current%.txt
  cat %datapath%\requeue-%current%.txt >> %datapath%\links-%current%.txt
  queueo %datapath%\links-%current%.txt > %datapath%\queue-%current%.txt
  nextlinks %datapath%\queue-%current%.txt %datapath%\rejects-%current%.txt > %datapath%\nextlinks-%current%.txt
  
  FOR /F "usebackq" %%A IN ('%datapath%\nextlinks-%current%.txt') DO set size=%%~zA

  if %size% LSS 10000 (
    goto borked 
  ) 
  
  set /A previous=current
  set /A current=current+1
  
  fetcho %datapath%\nextlinks-%previous%.txt %datapath%\requeue-%current%.txt > %datapath%\packet-%current%.xml
  echo %datapath%\packet-%current%.xml
goto loop

:borked
echo "It borked out - next links was <10000 bytes."