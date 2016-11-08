using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.ComponentModel;
using CYQ.Data.SQL;
using CYQ.Data.Cache;
using CYQ.Data.Table;

using CYQ.Data.Aop;
using CYQ.Data.Tool;
using CYQ.Data.UI;


namespace CYQ.Data
{
    /// <summary>
    /// Manipulate：table / view / custom statement
    ///<para>操作：表/视图/自定义语句</para>
    /// </summary>
    public partial class MAction : IDisposable
    {
        #region 全局变量

        internal DbBase dalHelper;//数据操作
        private SqlCreate _sqlCreate;
        private InsertOp _option = InsertOp.ID;
        private NoSqlAction _noSqlAction = null;
        private MDataRow _Data;//表示一行
        /// <summary>
        /// Archive the rows of the data structure
        /// <para>存档数据结构的行</para>
        /// </summary>
        public MDataRow Data
        {
            get
            {
                return _Data;
            }
        }
        /// <summary>
        /// 原始传进来的表名/视图名，未经过[多数据库转换处理]格式化。
        /// </summary>
        private string _sourceTableName;
        private string _TableName; //表名
        /// <summary>
        /// Table name (if the view statement is the operation, the final view of the name)
        ///<para>当前操作的表名(若操作的是视图语句，则为最后的视图名字</para> ）
        /// </summary>
        public string TableName
        {
            get
            {
                return _TableName;
            }
            set
            {
                _TableName = value;
            }
        }
        /// <summary>
        /// The database connection string
        ///<para>数据库链接字符串</para> 
        /// </summary>
        public string ConnectionString
        {
            get
            {
                if (dalHelper != null)
                {
                    return dalHelper.conn;
                }
                return string.Empty;
            }
        }
        private string _debugInfo = string.Empty;
        /// <summary>
        /// Get or set debugging information [need to set App Config.Debug.Open DebugInfo to ture]
        ///<para>获取或设置调试信息[需要设置AppConfig.Debug.OpenDebugInfo为ture]</para>
        /// </summary>
        public string DebugInfo
        {
            get
            {
                if (dalHelper != null)
                {
                    return dalHelper.debugInfo.ToString();
                }
                return _debugInfo;
            }
            set
            {
                if (dalHelper != null)
                {
                    dalHelper.debugInfo.Length = 0;
                    dalHelper.debugInfo.Append(value);
                }
            }
        }
        /// <summary>
        /// The database type
        /// <para>数据库类型</para>
        /// </summary>
        public DalType DalType
        {
            get
            {
                return dalHelper.dalType;
            }
        }
        /// <summary>
        /// The database name
        /// <para>数据库名称</para>
        /// </summary>
        public string DataBase
        {
            get
            {
                return dalHelper.DataBase;
            }
        }
        /// <summary>
        /// The database version
        /// 数据库的版本号
        /// </summary>
        public string DalVersion
        {
            get
            {
                return dalHelper.Version;
            }
        }
        /// <summary>
        /// The number of rows affected when executing a SQL command (-2 is an exception)
        /// <para>执行SQL命令时受影响的行数（-2则为异常）。</para>
        /// </summary>
        public int RecordsAffected
        {
            get
            {
                return dalHelper.recordsAffected;
            }
        }
        /// <summary>
        /// Command Timeout[seconds]
        ///<para>命令超时设置[单位秒]</para>
        /// </summary>
        public int TimeOut
        {
            get
            {
                if (dalHelper.Com != null)
                {
                    return dalHelper.Com.CommandTimeout;
                }
                return -1;
            }
            set
            {
                if (dalHelper.Com != null)
                {
                    dalHelper.Com.CommandTimeout = value;
                }
            }
        }
        private bool _AllowInsertID = false;
        /// <summary>
        /// Whether to allow manual insertion of IDs for self-incrementing primary key identification
        ///<para>自增主键标识的，是否允许手动插入ID</para> 
        /// </summary>
        public bool AllowInsertID
        {
            get
            {
                return _AllowInsertID;
            }
            set
            {
                _AllowInsertID = value;
            }
        }
        private bool _isInsertCommand; //是否执行插入命令(区分于Update命令)

        private bool _setIdentityResult = true;
        /// <summary>
        /// MSSQL插入标识ID时开启此选项[事务开启时有效]
        /// </summary>
        internal void SetIdentityInsertOn()
        {
            _setIdentityResult = true;
            if (dalHelper != null && dalHelper.isOpenTrans)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.MsSql:
                    case DalType.Sybase:
                        if (_Data.Columns.FirstPrimary.IsAutoIncrement)//数字型
                        {
                            try
                            {
                                string lastTable = Convert.ToString(CacheManage.LocalInstance.Get("MAction_IdentityInsertForSql"));
                                if (!string.IsNullOrEmpty(lastTable))
                                {
                                    lastTable = "set identity_insert " + SqlFormat.Keyword(lastTable, dalHelper.dalType) + " off";
                                    dalHelper.ExeNonQuery(lastTable, false);
                                }
                                _setIdentityResult = dalHelper.ExeNonQuery("set identity_insert " + SqlFormat.Keyword(_TableName, dalHelper.dalType) + " on", false) > -2;
                                if (_setIdentityResult)
                                {
                                    CacheManage.LocalInstance.Set("MAction_IdentityInsertForSql", _TableName, 30);
                                }
                            }
                            catch
                            {
                            }
                        }
                        break;
                }

            }
            _AllowInsertID = true;
        }
        /// <summary>
        /// MSSQL插入标识ID后关闭此选项[事务开启时有效]
        /// </summary>;
        internal void SetIdentityInsertOff()
        {
            if (_setIdentityResult && dalHelper != null && dalHelper.isOpenTrans)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.MsSql:
                    case DalType.Sybase:
                        if (_Data.Columns.FirstPrimary.IsAutoIncrement)//数字型
                        {
                            try
                            {
                                if (dalHelper.ExeNonQuery("set identity_insert " + SqlFormat.Keyword(_TableName, DalType.MsSql) + " off", false) > -2)
                                {
                                    _setIdentityResult = false;
                                    CacheManage.LocalInstance.Remove("MAction_IdentityInsertForSql");
                                }
                            }
                            catch
                            {
                            }
                        }
                        break;
                }

            }
            _AllowInsertID = false;
        }
        /// <summary>
        /// 当DeleteField被设置后（删除转更新操作），如果仍想删除操作（可将此属性置为true）
        /// </summary>
        internal bool IsIgnoreDeleteField = false;
        #endregion

        #region 构造函数

        /// <summary>
        /// Instantiation
        /// <para>实例化</para>
        /// </summary>
        /// <param name="tableNamesEnum">Parameters: table name, view, custom statement, DataRow
        /// <para>参数：表名、视图、自定义语句、MDataRow</para></param>
        public MAction(object tableNamesEnum)
        {
            Init(tableNamesEnum, AppConfig.DB.DefaultConn);
        }

        /// <param name="conn">Database connection statement or configuration key
        /// <para>数据库链接语句或配置Key</para></param>
        public MAction(object tableNamesEnum, string conn)
        {
            Init(tableNamesEnum, conn);
        }
        #endregion

        #region 初始化

        private void Init(object tableObj, string conn)
        {
            tableObj = SqlCreate.SqlToViewSql(tableObj);
            string dbName = StaticTool.GetDbName(ref tableObj);
            if (conn == AppConfig.DB.DefaultConn && !string.IsNullOrEmpty(dbName))
            {
                conn = dbName + "Conn";
            }
            MDataRow newRow;
            InitConn(tableObj, conn, out newRow);//尝试从MDataRow提取新的Conn链接
            InitSqlHelper(newRow, dbName);
            InitRowSchema(newRow, true);
            InitGlobalObject(true);
            //Aop.IAop myAop = Aop.InterAop.Instance.GetFromConfig();//试图从配置文件加载自定义Aop
            //if (myAop != null)
            //{
            //    SetAop(myAop);
            //}
        }
        private void InitConn(object tableObj, string conn, out MDataRow newRow)
        {
            if (tableObj is MDataRow)
            {
                newRow = tableObj as MDataRow;
            }
            else
            {
                newRow = new MDataRow();
                newRow.TableName = SqlFormat.NotKeyword(tableObj.ToString());
            }
            if (!string.IsNullOrEmpty(conn))
            {
                newRow.Conn = conn;
            }
            _sourceTableName = newRow.TableName;
        }
        private void InitSqlHelper(MDataRow newRow, string newDbName)
        {
            if (dalHelper == null)// || newCreate
            {
                dalHelper = DalCreate.CreateDal(newRow.Conn);
                //创建错误事件。
                if (dalHelper.IsOnExceptionEventNull)
                {
                    dalHelper.OnExceptionEvent += new DbBase.OnException(_DataSqlHelper_OnExceptionEvent);
                }
            }
            else
            {
                dalHelper.ClearParameters();//oracle 11g(某用户的电脑上会出问题，切换表操作，参数未清）
            }
            if (!string.IsNullOrEmpty(newDbName))//需要切换数据库。
            {
                if (string.Compare(dalHelper.DataBase, newDbName, StringComparison.OrdinalIgnoreCase) != 0)//数据库名称不相同。
                {
                    if (newRow.TableName.Contains(" "))//视图语句，则直接切换数据库链接。
                    {
                        dalHelper.ChangeDatabase(newDbName);
                    }
                    else
                    {
                        bool isWithDbName = newRow.TableName.Contains(".");//是否DBName.TableName
                        string fullTableName = isWithDbName ? newRow.TableName : newDbName + "." + newRow.TableName;
                        string sourceDbName = dalHelper.DataBase;
                        DbResetResult result = dalHelper.ChangeDatabaseWithCheck(fullTableName);
                        switch (result)
                        {
                            case DbResetResult.Yes://数据库切换了 (不需要前缀）
                            case DbResetResult.No_SaveDbName:
                            case DbResetResult.No_DBNoExists:
                                if (isWithDbName) //带有前缀的，取消前缀
                                {
                                    _sourceTableName = newRow.TableName = SqlFormat.NotKeyword(fullTableName);
                                }
                                break;
                            case DbResetResult.No_Transationing:
                                if (!isWithDbName)//如果不同的数据库，需要带有数据库前缀
                                {
                                    _sourceTableName = newRow.TableName = fullTableName;
                                }
                                break;
                        }
                    }
                }
            }
        }
        void _DataSqlHelper_OnExceptionEvent(string msg)
        {
            _aop.OnError(msg);
        }
        private static DateTime lastGCTime = DateTime.Now;
        private void InitRowSchema(MDataRow row, bool resetState)
        {
            _Data = row;
            _TableName = SqlCompatible.Format(_sourceTableName, dalHelper.dalType);
            _TableName = DBTool.GetMapTableName(dalHelper.conn, _TableName);//处理数据库映射兼容。
            if (_Data.Count == 0)
            {
                if (!TableSchema.FillTableSchema(ref _Data, ref dalHelper, _TableName, _sourceTableName))
                {
                    if (!dalHelper.TestConn())
                    {
                        Error.Throw(dalHelper.dalType + "." + dalHelper.DataBase + ":open database failed! check the connectionstring is be ok!\r\nerror:" + dalHelper.debugInfo.ToString());
                    }
                    Error.Throw(dalHelper.dalType + "." + dalHelper.DataBase + ":check the tablename  \"" + _TableName + "\" is exist?\r\nerror:" + dalHelper.debugInfo.ToString());
                }
            }
            else if (resetState)
            {
                _Data.SetState(0);
            }
            _Data.Conn = row.Conn;//FillTableSchema会改变_Row的对象。
        }


        /// <summary>
        /// Toggle Table Action: To switch between other tables, use this method
        /// <para>切换表操作：如需操作其它表，通过此方法切换</para>
        /// </summary>
        /// <param name="tableObj">Parameters: table name, view, custom statement, DataRow
        /// <para>参数：表名、视图、自定义语句、MDataRow</para></param>
        public void ResetTable(object tableObj)
        {
            ResetTable(tableObj, true, null);
        }

        /// <param name="resetState">Reset Row State (defaultValue:true)
        /// <para>是否重置原有的数据状态（默认true)</para></param>
        public void ResetTable(object tableObj, bool resetState)
        {
            ResetTable(tableObj, resetState, null);
        }
        /// <param name="newDbName">Other DataBaseName
        /// <para>其它数据库名称</para></param>
        public void ResetTable(object tableObj, bool resetState, string newDbName)
        {
            tableObj = SqlCreate.SqlToViewSql(tableObj);
            newDbName = newDbName ?? StaticTool.GetDbName(ref tableObj);
            MDataRow newRow;
            InitConn(tableObj, string.Empty, out newRow);

            //newRow.Conn = newDbName;//除非指定链接，否则不切换数据库
            InitSqlHelper(newRow, newDbName);
            InitRowSchema(newRow, resetState);
            InitGlobalObject(false);
        }

        private void InitGlobalObject(bool allowCreate)
        {
            if (_Data != null)
            {
                if (_sqlCreate == null)
                {
                    _sqlCreate = new SqlCreate(this);
                }
                if (_UI != null)
                {
                    _UI._Data = _Data;
                }
                else if (allowCreate)
                {
                    _UI = new MActionUI(ref _Data, dalHelper, _sqlCreate);
                }
                if (_noSqlAction != null)
                {
                    _noSqlAction.Reset(ref _Data, _TableName, dalHelper.Con.DataSource, dalHelper.dalType);
                }
                else if (allowCreate)
                {
                    switch (dalHelper.dalType)
                    {
                        case DalType.Txt:
                        case DalType.Xml:
                            _noSqlAction = new NoSqlAction(ref _Data, _TableName, dalHelper.Con.DataSource, dalHelper.dalType);
                            break;
                    }
                }
            }
        }

        #endregion

        #region 数据库操作方法

        private bool InsertOrUpdate(string sqlCommandText)
        {
            bool returnResult = false;
            if (_sqlCreate.isCanDo)
            {
                if (_isInsertCommand) //插入
                {
                    _isInsertCommand = false;
                    object ID;
                    switch (dalHelper.dalType)
                    {
                        case DalType.MsSql:
                        case DalType.Sybase:
                            ID = dalHelper.ExeScalar(sqlCommandText, false);
                            if (ID == null && AllowInsertID && dalHelper.recordsAffected > -2)
                            {
                                ID = _Data.PrimaryCell.Value;
                            }
                            break;
                        default:
                            #region MyRegion
                            bool isTrans = dalHelper.isOpenTrans;
                            int groupID = DataType.GetGroup(_Data.PrimaryCell.Struct.SqlType);
                            bool isNum = groupID == 1 && _Data.PrimaryCell.Struct.Scale <= 0;
                            if (!isTrans && (isNum || _Data.PrimaryCell.Struct.IsAutoIncrement) && (!AllowInsertID || _Data.PrimaryCell.IsNullOrEmpty)) // 数字自增加
                            {
                                dalHelper.isOpenTrans = true;//开启事务。
                                dalHelper.tranLevel = IsolationLevel.ReadCommitted;//默认事务级别已是这个，还是设置一下，避免外部调整对此的影响。
                            }
                            ID = dalHelper.ExeNonQuery(sqlCommandText, false);//返回的是受影响的行数
                            if (_option != InsertOp.None && ID != null && Convert.ToInt32(ID) > 0)
                            {
                                if (AllowInsertID && !_Data.PrimaryCell.IsNullOrEmpty)//手工插ID
                                {
                                    ID = _Data.PrimaryCell.Value;
                                }
                                else if (isNum)
                                {
                                    ClearParameters();
                                    ID = dalHelper.ExeScalar(_sqlCreate.GetMaxID(), false);
                                }
                                else
                                {
                                    ID = null;
                                    returnResult = true;
                                }

                            }
                            if (!isTrans)
                            {
                                dalHelper.EndTransaction();
                            }
                            #endregion
                            break;
                    }
                    if ((ID != null && Convert.ToString(ID) != "-2") || (dalHelper.recordsAffected > -2 && _option == InsertOp.None))
                    {
                        if (_option != InsertOp.None)
                        {
                            _Data.PrimaryCell.Value = ID;
                        }
                        returnResult = (_option == InsertOp.Fill) ? Fill(ID) : true;
                    }
                }
                else //更新
                {
                    returnResult = dalHelper.ExeNonQuery(sqlCommandText, false) > 0;
                }
            }
            else if (!_isInsertCommand && _Data.GetState() == 1 && dalHelper.recordsAffected != -2) // 更新操作。
            {
                //输出警告信息
                return true;
            }
            return returnResult;
        }

        #region 插入

        /// <summary>
        /// Insert To DataBase
        ///  <para>插入数据</para>
        /// </summary>
        public bool Insert()
        {
            return Insert(false, _option);
        }

        /// <param name="option">InsertOp
        /// <para>插入选项</para></param>
        public bool Insert(InsertOp option)
        {
            return Insert(false, option);
        }


        /// <param name="autoSetValue">Automatic get values from context
        /// <para>自动取值（从上下文环境中）</para></param>
        public bool Insert(bool autoSetValue)
        {
            return Insert(autoSetValue, _option);
        }

        /// <param name="option">InsertOp
        /// <para>插入选项</para></param>
        public bool Insert(bool autoSetValue, InsertOp option)
        {
            CheckDisposed();
            if (autoSetValue)
            {
                _UI.GetAll(!AllowInsertID);//允许插入ID时，也需要获取主键。
            }
            AopResult aopResult = AopResult.Default;
            if (_aop.IsLoadAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.AutoSetValue = autoSetValue;
                _aop.Para.InsertOp = option;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Insert);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.IsSuccess = _noSqlAction.Insert(dalHelper.isOpenTrans);
                        break;
                    default:
                        ClearParameters();
                        string sql = _sqlCreate.GetInsertSql();
                        _isInsertCommand = true;
                        _option = option;
                        _aop.Para.IsSuccess = InsertOrUpdate(sql);
                        break;
                }
            }
            else if (option != InsertOp.None)
            {
                _Data = _aop.Para.Row;
                InitGlobalObject(false);
            }
            if (_aop.IsLoadAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.Insert);
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            return _aop.Para.IsSuccess;
        }
        #endregion

        #region 更新
        /// <summary>
        /// Update to database [Will automatically try to fetch from the UI when conditions are not passed]
        /// <para>更新数据[不传where条件时将自动尝试从UI获取]</para>
        /// </summary>
        public bool Update()
        {
            return Update(null, true);
        }


        /// <param name="where">Sql statement where the conditions: 88, "id = 88"
        /// <para>sql语句的where条件：88、"id=88"</para></param>
        public bool Update(object where)
        {
            return Update(where, false);
        }


        /// <param name="autoSetValue">Automatic get values from context
        /// <para>自动取值（从上下文环境中）</para></param>
        public bool Update(bool autoSetValue)
        {
            return Update(null, autoSetValue);
        }

        /// <param name="autoSetValue">Automatic get values from context
        /// <para>自动取值（从上下文环境中）</para></param>
        public bool Update(object where, bool autoSetValue)
        {
            CheckDisposed();
            if (autoSetValue)
            {
                _UI.GetAll(false);
            }
            if (where == null || Convert.ToString(where) == "")
            {
                where = _sqlCreate.GetPrimaryWhere();
            }
            AopResult aopResult = AopResult.Default;
            if (_aop.IsLoadAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                //_aop.Para.AopPara = aopPara;
                _aop.Para.AutoSetValue = autoSetValue;
                _aop.Para.UpdateExpression = _sqlCreate.updateExpression;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Update);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.IsSuccess = _noSqlAction.Update(_sqlCreate.FormatWhere(where));
                        break;
                    default:
                        ClearParameters();
                        string sql = _sqlCreate.GetUpdateSql(where);
                        _aop.Para.IsSuccess = InsertOrUpdate(sql);
                        break;
                }
            }
            if (_aop.IsLoadAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.Update);
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            return _aop.Para.IsSuccess;
        }
        #endregion

        /// <summary>
        /// Delete from database [Will automatically try to fetch from the UI when conditions are not passed]
        /// <para>删除数据[不传where条件时将自动尝试从UI获取]</para>
        /// </summary>
        public bool Delete()
        {
            return Delete(null);
        }

        /// <param name="where">Sql statement where the conditions: 88, "id = 88"
        /// <para>sql语句的where条件：88、"id=88"</para></param>
        public bool Delete(object where)
        {
            CheckDisposed();
            if (where == null || Convert.ToString(where) == "")
            {
                _UI.PrimayAutoGetValue();
                where = _sqlCreate.GetPrimaryWhere();
            }
            AopResult aopResult = AopResult.Default;
            if (_aop.IsLoadAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                //_aop.Para.AopPara = aopPara;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Delete);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                string deleteField = AppConfig.DB.DeleteField;
                bool isToUpdate = !IsIgnoreDeleteField && !string.IsNullOrEmpty(deleteField) && _Data.Columns.Contains(deleteField);
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        string sqlWhere = _sqlCreate.FormatWhere(where);
                        if (isToUpdate)
                        {
                            _Data.Set(deleteField, true);
                            _aop.Para.IsSuccess = _noSqlAction.Update(sqlWhere);
                        }
                        else
                        {
                            _aop.Para.IsSuccess = _noSqlAction.Delete(sqlWhere);
                        }
                        break;
                    default:
                        ClearParameters();
                        string sql = isToUpdate ? _sqlCreate.GetDeleteToUpdateSql(where) : _sqlCreate.GetDeleteSql(where);
                        _aop.Para.IsSuccess = dalHelper.ExeNonQuery(sql, false) > 0;
                        break;
                }
            }
            if (_aop.IsLoadAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.Delete);
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            return _aop.Para.IsSuccess;
        }

        /// <summary>
        /// select all data
        ///<para>选择所有数据</para>
        /// </summary>
        public MDataTable Select()
        {
            int count;
            return Select(0, 0, null, out count);
        }

        /// <param name="where">Sql statement where the conditions: 88, "id = 88"
        /// <para>sql语句的where条件：88、"id=88"</para></param>
        public MDataTable Select(object where)
        {
            int count;
            return Select(0, 0, where, out count);
        }
        public MDataTable Select(int topN, object where)
        {
            int count;
            return Select(0, topN, where, out count);
        }
        /// <param name="pageIndex">pageIndex<para>第几页</para></param>
        /// <param name="pageSize">pageSize<para>每页数量[为0时默认选择所有]</para></param>
        public MDataTable Select(int pageIndex, int pageSize)
        {
            int count;
            return Select(pageIndex, pageSize, null, out count);
        }
        public MDataTable Select(int pageIndex, int pageSize, object where)
        {
            int count;
            return Select(pageIndex, pageSize, where, out count);
        }

        /// <param name="rowCount">The total number of records returned
        /// <para>返回的记录总数</para></param>
        public MDataTable Select(int pageIndex, int pageSize, object where, out int rowCount)
        {
            CheckDisposed();
            rowCount = 0;
            AopResult aopResult = AopResult.Default;
            if (_aop.IsLoadAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.PageIndex = pageIndex;
                _aop.Para.PageSize = pageSize;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                _aop.Para.SelectColumns = _sqlCreate.selectColumns;
                //_aop.Para.AopPara = aopPara;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Select);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                string primaryKey = SqlFormat.Keyword(_Data.Columns.FirstPrimary.ColumnName, dalHelper.dalType);//主键列名。
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.Table = _noSqlAction.Select(pageIndex, pageSize, _sqlCreate.FormatWhere(where), out rowCount, _sqlCreate.selectColumns);
                        break;
                    default:
                        _aop.Para.Table = new MDataTable(_TableName.Contains("(") ? "SysDefaultCustomTable" : _TableName);
                        _aop.Para.Table.LoadRow(_Data);
                        ClearParameters();//------------------------参数清除
                        DbDataReader sdReader = null;
                        string whereSql = string.Empty;//已格式化过的原生whereSql语句
                        if (_sqlCreate != null)
                        {
                            whereSql = _sqlCreate.FormatWhere(where);
                        }
                        else
                        {
                            whereSql = SqlFormat.Compatible(where, dalHelper.dalType, dalHelper.Com.Parameters.Count == 0);
                        }
                        bool byPager = pageIndex > 0 && pageSize > 0;//分页查询(第一页也要分页查询，因为要计算总数）
                        if (byPager && AppConfig.DB.PagerBySelectBase && dalHelper.dalType == DalType.MsSql && !dalHelper.Version.StartsWith("08"))// || dalHelper.dalType == DalType.Oracle
                        {
                            #region 存储过程执行
                            if (dalHelper.Com.Parameters.Count > 0)
                            {
                                dalHelper.debugInfo.Append(AppConst.HR + "error : select method deny call SetPara() method to add custom parameters!");
                            }
                            dalHelper.AddParameters("@PageIndex", pageIndex, DbType.Int32, -1, ParameterDirection.Input);
                            dalHelper.AddParameters("@PageSize", pageSize, DbType.Int32, -1, ParameterDirection.Input);
                            dalHelper.AddParameters("@TableName", _sqlCreate.GetSelectTableName(ref whereSql), DbType.String, -1, ParameterDirection.Input);

                            whereSql = _sqlCreate.AddOrderByWithCheck(whereSql, primaryKey);

                            dalHelper.AddParameters("@Where", whereSql, DbType.String, -1, ParameterDirection.Input);
                            sdReader = dalHelper.ExeDataReader("SelectBase", true);
                            #endregion
                        }
                        else
                        {
                            #region SQL语句分页执行
                            if (byPager)
                            {
                                rowCount = GetCount(whereSql);//利用自动缓存，避免每次分页都要计算总数。
                                //rowCount = Convert.ToInt32(dalHelper.ExeScalar(_sqlCreate.GetCountSql(whereSql), false));//分页查询先记算总数
                            }
                            if (!byPager || (rowCount > 0 && (pageIndex - 1) * pageSize < rowCount))
                            {
                                string sql = SqlCreateForPager.GetSql(dalHelper.dalType, dalHelper.Version, pageIndex, pageSize, whereSql, SqlFormat.Keyword(_TableName, dalHelper.dalType), rowCount, _sqlCreate.GetColumnsSql(), primaryKey, _Data.PrimaryCell.Struct.IsAutoIncrement);
                                sdReader = dalHelper.ExeDataReader(sql, false);
                            }
                            #endregion
                        }
                        if (sdReader != null)
                        {
                            // _aop.Para.Table.ReadFromDbDataReader(sdReader);//内部有关闭。
                            _aop.Para.Table = sdReader;
                            if (!byPager)
                            {
                                rowCount = _aop.Para.Table.Rows.Count;
                            }
                            else if (dalHelper.dalType == DalType.MsSql && AppConfig.DB.PagerBySelectBase)
                            {
                                rowCount = dalHelper.ReturnValue;
                            }
                            _aop.Para.Table.RecordsAffected = rowCount;
                        }
                        else
                        {
                            _aop.Para.Table.Rows.Clear();//预防之前的插入操作产生了一个数据行。
                        }
                        _aop.Para.IsSuccess = _aop.Para.Table.Rows.Count > 0;
                        ClearParameters();//------------------------参数清除
                        break;
                }
            }
            else if (_aop.Para.Table.RecordsAffected > 0)
            {
                rowCount = _aop.Para.Table.RecordsAffected;//返回记录总数
            }
            if (_aop.IsLoadAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.Select);
            }
            _aop.Para.Table.TableName = TableName;//Aop从Json缓存加载时会丢失表名。
            _aop.Para.Table.Conn = _Data.Conn;
            //修正DataType和Size、Scale
            for (int i = 0; i < _aop.Para.Table.Columns.Count; i++)
            {
                MCellStruct msTable = _aop.Para.Table.Columns[i];
                MCellStruct ms = _Data.Columns[msTable.ColumnName];
                if (ms != null)
                {
                    msTable.Load(ms);
                }
            }
            if (_sqlCreate != null)
            {
                _sqlCreate.selectColumns = null;
            }
            return _aop.Para.Table;
        }

        /// <summary>
        /// Select top one row to fill this.Data
        /// <para>选择一行数据，并填充到Data属性[不传where条件时将自动尝试从UI获取]</para>
        /// </summary>
        public bool Fill()
        {
            return Fill(null);
        }

        /// <param name="where">Sql statement where the conditions: 88, "id = 88"
        /// <para>sql语句的where条件：88、"id=88"</para></param>
        public bool Fill(object where)
        {
            CheckDisposed();
            if (where == null || Convert.ToString(where) == "")
            {
                _UI.PrimayAutoGetValue();
                where = _sqlCreate.GetPrimaryWhere();
            }
            AopResult aopResult = AopResult.Default;
            if (_aop.IsLoadAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                _aop.Para.SelectColumns = _sqlCreate.selectColumns;
                //_aop.Para.AopPara = aopPara;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Fill);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.IsSuccess = _noSqlAction.Fill(_sqlCreate.FormatWhere(where));
                        break;
                    default:
                        ClearParameters();
                        MDataTable mTable = dalHelper.ExeDataReader(_sqlCreate.GetTopOneSql(where), false);
                        // dalHelper.ResetConn();//重置Slave
                        if (mTable != null && mTable.Rows.Count > 0)
                        {
                            _Data.Clear();//清掉旧值。
                            _Data.LoadFrom(mTable.Rows[0], RowOp.None, true);//setselectcolumn("aa as bb")时
                            _aop.Para.IsSuccess = true;
                        }
                        else
                        {
                            _aop.Para.IsSuccess = false;
                        }
                        break;
                }
            }
            else if (_aop.Para.IsSuccess)
            {
                _Data.Clear();//清掉旧值。
                _Data.LoadFrom(_aop.Para.Row, RowOp.None, true);
            }

            if (_aop.IsLoadAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.Para.Row = _Data;
                _aop.End(Aop.AopEnum.Fill);
            }
            if (_aop.Para.IsSuccess)
            {
                if (_sqlCreate.selectColumns != null)
                {
                    string name;
                    string[] items;
                    foreach (object columnName in _sqlCreate.selectColumns)
                    {
                        items = columnName.ToString().Split(' ');
                        name = items[items.Length - 1];
                        MDataCell cell = _Data[name];
                        if (cell != null)
                        {
                            cell.State = 1;
                        }
                    }
                    items = null;
                }
                else
                {
                    _Data.SetState(1, BreakOp.Null);//查询时，定位状态为1
                }
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            if (_sqlCreate != null)
            {
                _sqlCreate.selectColumns = null;
            }
            return _aop.Para.IsSuccess;
        }
        /// <summary>
        /// Returns the number of records
        /// <para>返回记录数</para>
        /// </summary>
        public int GetCount()
        {
            return GetCount(null);
        }

        /// <param name="where">Sql statement where the conditions: 88, "id = 88"
        /// <para>sql语句的where条件：88、"id=88"</para></param>
        public int GetCount(object where)
        {
            CheckDisposed();
            AopResult aopResult = AopResult.Default;
            if (_aop.IsLoadAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                // _aop.Para.AopPara = aopPara;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.GetCount);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.RowCount = _noSqlAction.GetCount(_sqlCreate.FormatWhere(where));
                        _aop.Para.IsSuccess = _aop.Para.RowCount > 0;
                        break;
                    default:
                        ClearParameters();//清除系统参数
                        string countSql = _sqlCreate.GetCountSql(where);
                        object result = dalHelper.ExeScalar(countSql, false);

                        _aop.Para.IsSuccess = result != null;
                        if (_aop.Para.IsSuccess)
                        {
                            _aop.Para.RowCount = Convert.ToInt32(result);
                        }
                        else
                        {
                            _aop.Para.RowCount = -1;
                        }

                        //ClearSysPara(); //清除内部自定义参数[FormatWhere带自定义参数]
                        break;
                }
            }
            if (_aop.IsLoadAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.GetCount);
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            return _aop.Para.RowCount;
        }
        /// <summary>
        /// Whether or not the specified condition exists
        /// <para>是否存在指定条件的数据</para>
        /// </summary>
        /// <param name="where">Sql statement where the conditions: 88, "id = 88"
        /// <para>sql语句的where条件：88、"id=88"</para></param>
        public bool Exists(object where)
        {
            CheckDisposed();
            switch (dalHelper.dalType)
            {
                case DalType.Txt:
                case DalType.Xml:
                    return _noSqlAction.Exists(_sqlCreate.FormatWhere(where));
                default:
                    return GetCount(where) > 0;
            }
        }
        #endregion

        #region 其它方法



        /// <summary>
        /// Get value from this.Data
        /// <para>从Data属性里取值</para>
        /// </summary>
        public T Get<T>(object key)
        {
            return _Data.Get<T>(key);
        }


        /// <param name="defaultValue">defaultValue<para>值为Null时的默认替换值</para></param>
        public T Get<T>(object key, T defaultValue)
        {
            return _Data.Get<T>(key, defaultValue);
        }
        /// <summary>
        /// Set value to this.Data
        /// <para>为Data属性设置值</para>
        /// </summary>
        /// <param name="key">columnName<para>列名</para></param>
        /// <param name="value">value<para>值</para></param>
        public MAction Set(object key, object value)
        {
            return Set(key, value, -1);
        }

        /// <param name="state">set value state (0: unchanged; 1: assigned, the same value [insertable]; 2: assigned, different values [updateable])
        /// <para>设置值状态[0:未更改；1:已赋值,值相同[可插入]；2:已赋值,值不同[可更新]]</para></param>
        public MAction Set(object key, object value, int state)
        {
            MDataCell cell = _Data[key];
            if (cell != null)
            {
                cell.Value = value;
                if (state > 0 && state < 3)
                {
                    cell.State = state;
                }
            }
            else
            {
                dalHelper.debugInfo.Append(AppConst.HR + "Alarm : can't find the ColumnName:" + key);
            }
            return this;
        }
        /// <summary>
        /// Sets a custom expression for the Update operation.
        /// <para>为Update操作设置自定义表达式。</para>
        /// </summary>
        /// <param name="updateExpression">as："a=a+1"<para>如："a=a+1"</para></param>
        public MAction SetExpression(string updateExpression)
        {
            _sqlCreate.updateExpression = updateExpression;
            return this;
        }
        /// <summary>
        /// Parameterized pass [used when the Where condition is a parameterized (such as: name = @ name) statement]
        /// <para>参数化传参[当Where条件为参数化(如：name=@name)语句时使用]</para>
        /// </summary>
        /// <param name="paraName">paraName<para>参数名称</para></param>
        /// <param name="value">value<para>参数值</para></param>
        public MAction SetPara(object paraName, object value)
        {
            return SetPara(paraName, value, DbType.String);
        }
        List<AopCustomDbPara> customParaNames = new List<AopCustomDbPara>();

        /// <param name="dbType">dbType<para>参数类型</para></param>
        public MAction SetPara(object paraName, object value, DbType dbType)
        {
            if (dalHelper.AddParameters(Convert.ToString(paraName), value, dbType, -1, ParameterDirection.Input))
            {
                AopCustomDbPara para = new AopCustomDbPara();
                para.ParaName = Convert.ToString(paraName).Replace(":", "").Replace("@", "");
                para.Value = value;
                para.ParaDbType = dbType;
                customParaNames.Add(para);
                if (_aop.IsLoadAop)
                {
                    _aop.Para.CustomDbPara = customParaNames;
                }
            }
            return this;
        }
        
        /// <summary>
        /// Aop scenes can only be used by the parameterization of the Senate
        /// <para>Aop场景才可能使用的参数化传参</para>
        /// </summary>
        /// <param name="customParas">Paras<para>参数列表</para></param>
        public MAction SetPara(List<AopCustomDbPara> customParas)
        {
            if (customParas != null && customParas.Count > 0)
            {
                foreach (AopCustomDbPara para in customParas)
                {
                    SetPara(para.ParaName, para.Value, para.ParaDbType);
                }
            }
            return this;
        }
        /// <summary>
        /// Clears paras (from SetPara method)
        /// <para>清除(SetPara设置的)自定义参数</para>
        /// </summary>
        public void ClearPara()
        {
            if (customParaNames.Count > 0)
            {
                if (dalHelper != null && dalHelper.Com.Parameters.Count > 0)
                {
                    string paraName = string.Empty;
                    foreach (AopCustomDbPara item in customParaNames)
                    {
                        for (int i = dalHelper.Com.Parameters.Count - 1; i > -1; i--)
                        {
                            if (string.Compare(dalHelper.Com.Parameters[i].ParameterName.TrimStart(dalHelper.Pre), item.ParaName.ToString()) == 0)
                            {
                                dalHelper.Com.Parameters.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
                customParaNames.Clear();
            }
        }
       

        /// <summary>
        /// 清除系统参数[保留自定义参数]
        /// </summary>
        private void ClearParameters()
        {
            if (dalHelper != null)
            {
                if (customParaNames.Count > 0)//带有自定义参数
                {
                    if (dalHelper.Com.Parameters.Count > 0)
                    {
                        bool isBreak = false;
                        for (int i = dalHelper.Com.Parameters.Count - 1; i > -1; i--)
                        {
                            for (int j = 0; j < customParaNames.Count; j++)
                            {
                                if (string.Compare(dalHelper.Com.Parameters[i].ParameterName.TrimStart(dalHelper.Pre), customParaNames[j].ParaName.ToString()) == 0)
                                {
                                    isBreak = true;
                                }
                            }
                            if (!isBreak)
                            {
                                dalHelper.Com.Parameters.RemoveAt(i);
                                isBreak = false;
                            }
                        }
                    }
                }
                else
                {
                    dalHelper.ClearParameters();
                }
            }
        }

        /// <summary>
        /// Specifies the column to read
        /// <para>指定读取的列</para>
        /// </summary>
        /// <param name="columnNames">as："columnA","columnB as B"</param>
        public MAction SetSelectColumns(params object[] columnNames)
        {
            _sqlCreate.selectColumns = columnNames;
            return this;
        }

        /// <summary>
        ///  Get where statement
        /// <para>根据元数据列组合where条件。</para>
        /// </summary>
       
        public string GetWhere(bool isAnd, params MDataCell[] cells)
        {
            return SqlCreate.GetWhere(DalType, isAnd, cells);
        }

        /// <param name="isAnd">connect by and/or<para>true为and连接，反之为or链接</para></param>
        /// <param name="cells">MDataCell<para>单元格</para></param>
        public string GetWhere(params MDataCell[] cells)
        {
            return SqlCreate.GetWhere(DalType, cells);
        }



        #endregion

        #region 事务操作
        /// <summary>
        /// Set the transaction level
        /// <para>设置事务级别</para>
        /// </summary>
        /// <param name="level">IsolationLevel</param>
        public MAction SetTransLevel(IsolationLevel level)
        {
            dalHelper.tranLevel = level;
            return this;
        }

        /// <summary>
        /// Begin Transation
        /// <para>开始事务</para>
        /// </summary>
        public void BeginTransation()
        {
            dalHelper.isOpenTrans = true;
        }
        /// <summary>
        /// Commit Transation
        /// <para>提交事务</para>
        /// </summary>
        public bool EndTransation()
        {
            if (dalHelper != null && dalHelper.isOpenTrans)
            {
                return dalHelper.EndTransaction();
            }
            return false;
        }
        /// <summary>
        /// RollBack Transation
        /// <para>事务回滚</para>
        /// </summary>
        public bool RollBack()
        {
            if (dalHelper != null && dalHelper.isOpenTrans)
            {
                return dalHelper.RollBack();
            }
            return false;
        }
        #endregion

        #region IDisposable 成员
        public void Dispose()
        {
            Dispose(false);
        }
        /// <summary>
        /// Dispose
        /// <para>释放资源</para>
        /// </summary>
        internal void Dispose(bool isonError)
        {
            hasDisposed = true;
            if (dalHelper != null)
            {
                if (!dalHelper.IsOnExceptionEventNull)
                {
                    dalHelper.OnExceptionEvent -= new DbBase.OnException(_DataSqlHelper_OnExceptionEvent);
                }
                _debugInfo = dalHelper.debugInfo.ToString();
                dalHelper.Dispose();
                if (!isonError)
                {
                    dalHelper = null;

                    if (_sqlCreate != null)
                    {
                        _sqlCreate = null;
                    }
                }
            }
            if (!isonError)
            {
                if (_noSqlAction != null)
                {
                    _noSqlAction.Dispose();
                }
                if (_aop != null)
                {
                    _aop = null;
                }
            }
        }
        internal void OnError()
        {
            if (dalHelper != null && dalHelper.isOpenTrans)
            {
                Dispose(true);
            }
        }
        bool hasDisposed = false;
        private void CheckDisposed()
        {
            if (hasDisposed)
            {
                Error.Throw("The current object 'MAction' has been disposed");
            }
        }
        #endregion


    }
    //AOP 部分
    public partial class MAction
    {
        #region Aop操作
        private InterAop _aop = new InterAop();
        /// <summary>
        /// Set Aop State
        /// <para>设置Aop状态</para>
        /// </summary>
        public MProc SetAopState(AopOp op)
        {
            _aop.aopOp = op;
            return this;
        }
        /// <summary>
        /// Pass additional parameters for Aop use
        /// <para>传递额外的参数供Aop使用</para>
        /// </summary>
        public MAction SetAopPara(object para)
        {
            _aop.Para.AopPara = para;
            return this;
        }

        #endregion
    }
    //UI 部分
    public partial class MAction
    {
        private MActionUI _UI;
        /// <summary>
        /// Manipulate UI
        /// <para>UI操作</para>
        /// </summary>
        public MActionUI UI
        {
            get
            {
                return _UI;
            }
        }
    }

}
