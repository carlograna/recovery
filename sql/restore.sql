restore database itemX
from disk = 'C:\temp\data\itemx.bak'
with file = 1
,move 'itemx_data'
 to 'C:\Users\carlo\Documents\Data\itemx_data.mdf'
,move 'itemx_log'
to 'C:\Users\carlo\Documents\Data\itemx_log.ldf'
,recovery
,nounload;