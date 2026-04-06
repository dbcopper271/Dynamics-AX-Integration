FUNCTION Z_MON_GET_TRANSPORT_QUEUE.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IV_TARGET_SYS)     TYPE  C  OPTIONAL  "(filter by target, e.g. 'PRD')
*"     VALUE(IV_MAX_ROWS)       TYPE  I  DEFAULT 200
*"  EXPORTING
*"     VALUE(EV_QUEUE_DEPTH)    TYPE  I   "(total pending transports)
*"     VALUE(EV_STATUS)         TYPE  C
*"     VALUE(EV_MESSAGE)        TYPE  STRING
*"  TABLES
*"     ET_TRANSPORTS            TYPE  TY_TRANSPORTS
*"  EXCEPTIONS
*"     SYSTEM_ERROR             1
*"----------------------------------------------------------------------
*
* RFC-enabled: YES
* Description: Returns transport requests pending in the import queue
*              for the target system. Reads E070 (header) + TMSBUFTRANS.
*
* Key tables:
*   E070       – Transport request header
*   E070C      – Transport request classification (target system)
*   TMSBUFTRANS – STMS import buffer
*
* Authorization: S_TRANSPRT (CTS authorization)
*----------------------------------------------------------------------

  CONSTANTS:
    lc_warn_threshold TYPE i VALUE 10,
    lc_crit_threshold TYPE i VALUE 30.

  DATA: ls_tp    TYPE ty_transport,
        lv_max   TYPE i.

  CLEAR: et_transports, ev_queue_depth, ev_status, ev_message.

  lv_max = COND #( WHEN iv_max_rows > 0 THEN iv_max_rows ELSE 200 ).

  TRY.
*   ── Read transport import buffer ────────────────────────────────────
*   TMSBUFTRANS: SYSNAM (target), TRKORR, TRSTATUS, AS4TEXT, AS4USER, etc.
*   Status codes:
*     ' ' = waiting / queued
*     'R' = running
*     'E' = error
*     'W' = warning

    IF iv_target_sys IS NOT INITIAL.
      SELECT trkorr, sysnam AS target_sys, trstatus, as4text, as4user,
             as4date, as4time
        FROM tmsbuftrans
        INTO TABLE @DATA(lt_buf)
        WHERE sysnam = @iv_target_sys
          AND ( trstatus = ' ' OR trstatus = 'R' OR trstatus = 'E' )
        ORDER BY as4date ASCENDING, as4time ASCENDING.          "#EC CI_NOWHERE
    ELSE.
      SELECT trkorr, sysnam AS target_sys, trstatus, as4text, as4user,
             as4date, as4time
        FROM tmsbuftrans
        INTO TABLE @lt_buf
        WHERE trstatus = ' ' OR trstatus = 'R' OR trstatus = 'E'
        ORDER BY as4date ASCENDING, as4time ASCENDING.          "#EC CI_NOWHERE
    ENDIF.

    IF sy-subrc <> 0 AND sy-subrc <> 4.
*     Fallback: read directly from E070 for requests in 'D' (modifiable) status
      SELECT e070~trkorr, e070~trfunction, e070~trstatus,
             e070~as4user, e070~as4date, e070~as4text
        FROM e070
        INNER JOIN e070c ON e070c~trkorr = e070~trkorr
        INTO TABLE @DATA(lt_e070)
        WHERE e070~trstatus = 'D'
          AND ( iv_target_sys IS INITIAL OR e070c~sysnam = @iv_target_sys )
        ORDER BY e070~as4date ASCENDING.                        "#EC CI_NOWHERE

      LOOP AT lt_e070 INTO DATA(ls_e070).
        CLEAR ls_tp.
        ls_tp-trkorr     = ls_e070-trkorr.
        ls_tp-as4text    = ls_e070-as4text.
        ls_tp-trstatus   = ls_e070-trstatus.
        ls_tp-trfunction = ls_e070-trfunction.
        ls_tp-as4user    = ls_e070-as4user.
        ls_tp-as4date    = ls_e070-as4date.
        ls_tp-target_sys = iv_target_sys.
        APPEND ls_tp TO et_transports.
        IF lines( et_transports ) >= lv_max. EXIT. ENDIF.
      ENDLOOP.
    ELSE.
*     Map from TMSBUFTRANS results
      LOOP AT lt_buf INTO DATA(ls_buf).
        CLEAR ls_tp.
        ls_tp-trkorr     = ls_buf-trkorr.
        ls_tp-as4text    = ls_buf-as4text.
        ls_tp-trstatus   = ls_buf-trstatus.
        ls_tp-as4user    = ls_buf-as4user.
        ls_tp-as4date    = ls_buf-as4date.
        ls_tp-target_sys = ls_buf-target_sys.
        APPEND ls_tp TO et_transports.
        IF lines( et_transports ) >= lv_max. EXIT. ENDIF.
      ENDLOOP.
    ENDIF.

    ev_queue_depth = lines( et_transports ).

*   ── Determine status ────────────────────────────────────────────────
    " Count error transports
    DATA(lv_error_count) = 0.
    LOOP AT et_transports INTO ls_tp WHERE trstatus = 'E'.
      lv_error_count = lv_error_count + 1.
    ENDLOOP.

    IF lv_error_count > 0.
      ev_status  = gc_unhealthy.
      ev_message = |{ lv_error_count } transport(s) in ERROR state; |
                && |queue depth: { ev_queue_depth }|.
    ELSEIF ev_queue_depth >= lc_crit_threshold.
      ev_status  = gc_unhealthy.
      ev_message = |Transport queue depth { ev_queue_depth } |
                && |exceeds critical threshold { lc_crit_threshold }|.
    ELSEIF ev_queue_depth >= lc_warn_threshold.
      ev_status  = gc_degraded.
      ev_message = |Transport queue depth { ev_queue_depth } |
                && |exceeds warning threshold { lc_warn_threshold }|.
    ELSE.
      ev_status  = gc_healthy.
      ev_message = |Transport queue OK: { ev_queue_depth } request(s) pending|.
    ENDIF.

  CATCH cx_root INTO DATA(lx).
    ev_status  = gc_unhealthy.
    ev_message = lx->get_text( ).
    RAISE system_error.
  ENDTRY.

ENDFUNCTION.
