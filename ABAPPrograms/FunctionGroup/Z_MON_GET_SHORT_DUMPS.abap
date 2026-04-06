FUNCTION Z_MON_GET_SHORT_DUMPS.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IV_WINDOW_MINUTES) TYPE  I  DEFAULT 60
*"     VALUE(IV_MAX_ROWS)       TYPE  I  DEFAULT 100
*"  EXPORTING
*"     VALUE(EV_DUMP_COUNT)     TYPE  I
*"     VALUE(EV_STATUS)         TYPE  C
*"     VALUE(EV_MESSAGE)        TYPE  STRING
*"  TABLES
*"     ET_SHORT_DUMPS           TYPE  TY_SHORT_DUMPS
*"  EXCEPTIONS
*"     SYSTEM_ERROR             1
*"----------------------------------------------------------------------
*
* RFC-enabled: YES
* Description: Reads ABAP runtime errors (short dumps) from table SNAPSHOTS
*              within the given time window.
*
* Tables accessed: SNAPSHOTS (short dump header), SNAP_ADDITIONAL
* Authorization:   S_ABAP_DMP (authorization for short dump analysis)
*----------------------------------------------------------------------

  CONSTANTS:
    lc_warn_threshold TYPE i VALUE 5,
    lc_crit_threshold TYPE i VALUE 20.

  DATA: lv_from_date  TYPE d,
        lv_from_time  TYPE t,
        lv_from_ts    TYPE timestamp,
        ls_dump       TYPE ty_short_dump,
        lv_minutes    TYPE i.

  CLEAR: et_short_dumps, ev_dump_count, ev_status, ev_message.

  " Default window
  lv_minutes = COND #( WHEN iv_window_minutes > 0
                       THEN iv_window_minutes
                       ELSE 60 ).

  TRY.
*   ── Calculate time window ───────────────────────────────────────────
    GET TIME STAMP FIELD DATA(lv_now_ts).

*   Subtract minutes to get start of window
    DATA(lv_offset_s) = lv_minutes * 60.
    lv_from_ts = lv_now_ts - lv_offset_s.

*   Convert to date/time for SNAPSHOTS query
    CONVERT TIME STAMP lv_from_ts TIME ZONE 'UTC'
      INTO DATE lv_from_date TIME lv_from_time.

*   ── Select short dumps from SNAPSHOTS table ─────────────────────────
*   SNAPSHOTS: MANDT / SNAPTIME (timestamp key) / DATUM / UZEIT /
*              USERNAME / PROGNAME / ERRTYP / ERRMSG
    SELECT mandt, snaptime, datum, uzeit,
           username, progname, errtyp, errmsg
      FROM snapshots
      INTO TABLE @DATA(lt_snaps)
      WHERE datum >= @lv_from_date
        AND uzeit >= @lv_from_time
      ORDER BY datum DESCENDING, uzeit DESCENDING.    "#EC CI_NOWHERE

    IF sy-subrc <> 0 AND sy-subrc <> 4.
      ev_status  = gc_unhealthy.
      ev_message = 'Cannot read SNAPSHOTS table'.
      RAISE system_error.
    ENDIF.

*   ── Limit rows for performance ──────────────────────────────────────
    DATA(lv_max) = COND #( WHEN iv_max_rows > 0
                           THEN iv_max_rows ELSE 100 ).

    DATA(lv_row) = 0.
    LOOP AT lt_snaps INTO DATA(ls_snap).
      lv_row = lv_row + 1.
      IF lv_row > lv_max. EXIT. ENDIF.

      CLEAR ls_dump.
      ls_dump-dump_id    = ls_snap-snaptime.
      ls_dump-mandt      = ls_snap-mandt.
      ls_dump-username   = ls_snap-username.
      ls_dump-progname   = ls_snap-progname.
      ls_dump-errtyp     = ls_snap-errtyp.
      ls_dump-errmsg     = ls_snap-errmsg.

*     Convert DATUM + UZEIT to UTC timestamp
      CONVERT DATE ls_snap-datum TIME ls_snap-uzeit
        TIME ZONE sy-zonlo
        INTO TIME STAMP ls_dump-occurred_at TIME ZONE 'UTC'.

      APPEND ls_dump TO et_short_dumps.
    ENDLOOP.

    ev_dump_count = lines( et_short_dumps ).

*   ── Determine status ────────────────────────────────────────────────
    IF ev_dump_count >= lc_crit_threshold.
      ev_status  = gc_unhealthy.
      ev_message = |{ ev_dump_count } short dumps in last { lv_minutes } min |
                && |(threshold: { lc_crit_threshold })|.
    ELSEIF ev_dump_count >= lc_warn_threshold.
      ev_status  = gc_degraded.
      ev_message = |{ ev_dump_count } short dumps in last { lv_minutes } min |
                && |(warning threshold: { lc_warn_threshold })|.
    ELSE.
      ev_status  = gc_healthy.
      ev_message = |{ ev_dump_count } short dump(s) in last { lv_minutes } min – OK|.
    ENDIF.

  CATCH cx_root INTO DATA(lx).
    ev_status  = gc_unhealthy.
    ev_message = lx->get_text( ).
    RAISE system_error.
  ENDTRY.

ENDFUNCTION.
