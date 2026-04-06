*&---------------------------------------------------------------------*
*& Include ZMON_TOP
*& Global data for Function Group ZMON
*&---------------------------------------------------------------------*
*
* Function Group : ZMON
* Description    : IT Monitoring – Backend RFC Function Modules
* Transport      : Z_MON_<SID>_001
*
* Change History:
*   Date        Developer   Description
*   ----------  ----------  -------------------------------------------
*   2026-04-06  ZZ_MONITOR  Initial creation
*---------------------------------------------------------------------*

INCLUDE Z_MON_TYPES.          " Shared type pool

" Common constants
CONSTANTS:
  gc_healthy   TYPE c LENGTH 10 VALUE 'HEALTHY',
  gc_degraded  TYPE c LENGTH 10 VALUE 'DEGRADED',
  gc_unhealthy TYPE c LENGTH 10 VALUE 'UNHEALTHY',
  gc_unknown   TYPE c LENGTH 10 VALUE 'UNKNOWN'.

" WP type codes
CONSTANTS:
  gc_wp_dia  TYPE c LENGTH 2 VALUE 'DI',   " Dialog
  gc_wp_btc  TYPE c LENGTH 2 VALUE 'BT',   " Batch
  gc_wp_spo  TYPE c LENGTH 2 VALUE 'SP',   " Spool
  gc_wp_upd  TYPE c LENGTH 2 VALUE 'UP',   " Update
  gc_wp_up2  TYPE c LENGTH 2 VALUE 'U2',   " Update 2
  gc_wp_enq  TYPE c LENGTH 2 VALUE 'EN'.   " Enqueue
