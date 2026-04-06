FUNCTION Z_MON_GET_IDOC_STATUS.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IV_WINDOW_MINUTES) TYPE  I          DEFAULT 60
*"     VALUE(IV_DIRECTION)      TYPE  C          OPTIONAL "(1=Out/2=In/blank=Both)
*"     VALUE(IV_MESTYP)         TYPE  EDIDC-MESTYP OPTIONAL
*"     VALUE(IV_MAX_ROWS)       TYPE  I          DEFAULT 500
*"  EXPORTING
*"     VALUE(EV_TOTAL_IDOCS)    TYPE  I
*"     VALUE(EV_ERROR_COUNT)    TYPE  I
*"     VALUE(EV_SUCCESS_COUNT)  TYPE  I
*"     VALUE(EV_ERROR_RATE_PCT) TYPE  P  DECIMALS 2
*"     VALUE(EV_STATUS)         TYPE  C
*"     VALUE(EV_MESSAGE)        TYPE  STRING
*"  TABLES
*"     ET_IDOC_ERRORS           TYPE  TY_IDOC_ERRORS
*"  EXCEPTIONS
*"     SYSTEM_ERROR             1
*"----------------------------------------------------------------------
*
* RFC-enabled: YES
* Description: Returns IDoc processing errors from EDIDC + EDIDS tables.
*
* Key IDoc error status codes (EDIDS-STATUS):
*   02 = Error passing data to port            (Outbound)
*   04 = Error within control information       (Outbound)
*   25 = Processing despite syntax errors       (Inbound)
*   26 = IDoc with errors added                 (Inbound)
*   51 = Application document not posted        (Inbound – ERROR)
*   56 = IDoc with errors added                 (Inbound)
*   60 = Error during syntax check of IDoc      (Inbound)
*   62 = IDoc passed to application             (OK)
*   53 = Application document posted            (OK)
*
* Authorization: B_ALE_RECV / B_ALE_SEND
*----------------------------------------------------------------------

  CONSTANTS:
    lc_warn_error_rate TYPE p DECIMALS 2 VALUE '5.00',   " 5%
    lc_crit_error_rate TYPE p DECIMALS 2 VALUE '20.00'.  " 20%

  " IDoc error status codes (not exhaustive – add more per business need)
  DATA: lt_error_statuses TYPE STANDARD TABLE OF char2 WITH DEFAULT KEY,
        ls_idoc_err       TYPE ty_idoc_error,
        lv_from_dt        TYPE d,
        lv_from_tm        TYPE t.

  CLEAR: et_idoc_errors, ev_total_idocs, ev_error_count,
         ev_success_count, ev_error_rate_pct, ev_status, ev_message.

  " Define error status codes
  APPEND '02' TO lt_error_statuses.
  APPEND '04' TO lt_error_statuses.
  APPEND '25' TO lt_error_statuses.
  APPEND '26' TO lt_error_statuses.
  APPEND '51' TO lt_error_statuses.
  APPEND '56' TO lt_error_statuses.
  APPEND '60' TO lt_error_statuses.

  DATA(lv_minutes) = COND #( WHEN iv_window_minutes > 0
                             THEN iv_window_minutes ELSE 60 ).

  TRY.
*   ── Calculate time window ────────────────────────────────────────────
    GET TIME STAMP FIELD DATA(lv_now_ts).
    DATA(lv_from_ts) = lv_now_ts - ( lv_minutes * 60 ).
    CONVERT TIME STAMP lv_from_ts TIME ZONE 'UTC'
      INTO DATE lv_from_dt TIME lv_from_tm.

*   ── Count all IDocs in window ─────────────────────────────────────────
    IF iv_direction IS NOT INITIAL AND iv_mestyp IS NOT INITIAL.
      SELECT COUNT(*) FROM edidc
        INTO @ev_total_idocs
        WHERE credat >= @lv_from_dt
          AND direct = @iv_direction
          AND mestyp = @iv_mestyp.
    ELSEIF iv_direction IS NOT INITIAL.
      SELECT COUNT(*) FROM edidc
        INTO @ev_total_idocs
        WHERE credat >= @lv_from_dt
          AND direct = @iv_direction.
    ELSE.
      SELECT COUNT(*) FROM edidc
        INTO @ev_total_idocs
        WHERE credat >= @lv_from_dt.
    ENDIF.

*   ── Select error IDocs ───────────────────────────────────────────────
    DATA(lv_max) = COND #( WHEN iv_max_rows > 0 THEN iv_max_rows ELSE 500 ).

    SELECT dc~docnum, dc~mestyp, dc~mescod, dc~mesfct,
           dc~direct, dc~sndprt, dc~sndprn, dc~rcvprt, dc~rcvprn,
           dc~credat, dc~cretim,
           ds~status
      FROM edidc AS dc
      INNER JOIN edids AS ds ON ds~docnum = dc~docnum
      INTO TABLE @DATA(lt_idoc_err)
      WHERE dc~credat >= @lv_from_dt
        AND ds~status IN @lt_error_statuses
        AND ( iv_direction IS INITIAL OR dc~direct = @iv_direction )
        AND ( iv_mestyp    IS INITIAL OR dc~mestyp = @iv_mestyp    )
      ORDER BY dc~credat DESCENDING, dc~cretim DESCENDING.      "#EC CI_NOWHERE

    DATA lv_row TYPE i.
    LOOP AT lt_idoc_err INTO DATA(ls_err_raw).
      lv_row = lv_row + 1.
      IF lv_row > lv_max. EXIT. ENDIF.

      CLEAR ls_idoc_err.
      ls_idoc_err-docnum    = ls_err_raw-docnum.
      ls_idoc_err-mestyp    = ls_err_raw-mestyp.
      ls_idoc_err-mescod    = ls_err_raw-mescod.
      ls_idoc_err-mesfct    = ls_err_raw-mesfct.
      ls_idoc_err-direction = ls_err_raw-direct.
      ls_idoc_err-status    = ls_err_raw-status.
      ls_idoc_err-sndprt    = ls_err_raw-sndprt.
      ls_idoc_err-sndprn    = ls_err_raw-sndprn.
      ls_idoc_err-rcvprt    = ls_err_raw-rcvprt.
      ls_idoc_err-rcvprn    = ls_err_raw-rcvprn.
      ls_idoc_err-credat    = ls_err_raw-credat.
      ls_idoc_err-cretim    = ls_err_raw-cretim.

*     Get status description text
      SELECT SINGLE descrp FROM edistatst
        INTO @ls_idoc_err-statustext
        WHERE status   = @ls_err_raw-status
          AND langu    = 'E'.
      IF sy-subrc <> 0.
        ls_idoc_err-statustext = |Status { ls_err_raw-status }|.
      ENDIF.

      APPEND ls_idoc_err TO et_idoc_errors.
    ENDLOOP.

    ev_error_count   = lines( et_idoc_errors ).
    ev_success_count = ev_total_idocs - ev_error_count.
    IF ev_success_count < 0. ev_success_count = 0. ENDIF.

*   ── Calculate error rate ─────────────────────────────────────────────
    IF ev_total_idocs > 0.
      ev_error_rate_pct = ( ev_error_count * 100 ) / ev_total_idocs.
    ENDIF.

*   ── Determine health status ──────────────────────────────────────────
    IF ev_error_rate_pct >= lc_crit_error_rate.
      ev_status  = gc_unhealthy.
      ev_message = |IDoc error rate { ev_error_rate_pct }% – CRITICAL |
                && |(threshold: { lc_crit_error_rate }%)|.
    ELSEIF ev_error_rate_pct >= lc_warn_error_rate.
      ev_status  = gc_degraded.
      ev_message = |IDoc error rate { ev_error_rate_pct }% – WARNING |
                && |(threshold: { lc_warn_error_rate }%)|.
    ELSEIF ev_error_count > 0.
      ev_status  = gc_degraded.
      ev_message = |{ ev_error_count } IDoc error(s) in last { lv_minutes } min |
                && |(rate: { ev_error_rate_pct }%)|.
    ELSE.
      ev_status  = gc_healthy.
      ev_message = |IDocs OK: { ev_total_idocs } processed, |
                && |0 errors in last { lv_minutes } min|.
    ENDIF.

  CATCH cx_root INTO DATA(lx).
    ev_status  = gc_unhealthy.
    ev_message = lx->get_text( ).
    RAISE system_error.
  ENDTRY.

ENDFUNCTION.
