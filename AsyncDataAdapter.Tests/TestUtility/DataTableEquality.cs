using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Shouldly;

namespace AsyncDataAdapter.Tests
{
    public static class DataTableEquality
    {
        public static Boolean DataSetEquals( DataSet left, DataSet right )
        {
            _ = left .ShouldNotBeNull();
            _ = right.ShouldNotBeNull();
            Object.ReferenceEquals( left, right ).ShouldBeFalse();

            //

            left.Tables.Count.ShouldBe( right.Tables.Count );

            for( Int32 t = 0; t < left.Tables.Count; t++ )
            {
                DataTable lt = left .Tables[t];
                DataTable rt = right.Tables[t];

                Boolean eq = DataTableEquals( lt, rt );
                if( !eq ) return false;
            }

            return true;
        }

        public static Boolean DataTableEquals( DataTable left, DataTable right )
        {
            _ = left .ShouldNotBeNull();
            _ = right.ShouldNotBeNull();
            Object.ReferenceEquals( left, right ).ShouldBeFalse(); // Ensure the tables are not the same object in memory.

            //

            left.Namespace.ShouldBe( right.Namespace );
            left.TableName.ShouldBe( right.TableName );

            left.Rows   .Count.ShouldBe( right.Rows   .Count );
            left.Columns.Count.ShouldBe( right.Columns.Count );

            for( Int32 y = 0; y < left.Rows.Count; y++ )
            {
                DataRow leftRow  = left .Rows[y];
                DataRow rightRow = right.Rows[y];

                for( Int32 x = 0; x < left.Columns.Count; x++ )
                {
                    Object leftValue  = leftRow [x];
                    Object rightValue = rightRow[x];

                    Boolean eq = CellsEquals( leftValue, rightValue );
                    if( !eq ) return false;
                }
            }

            return true;
        }

        public static Boolean CellsEquals( Object leftValue, Object rightValue )
        {
            if( leftValue is null && rightValue is null ) return true;
            if( leftValue is null || rightValue is null ) return false;

            Type leftType = leftValue .GetType();
            Type righType = rightValue.GetType();

            if( leftType.Equals( righType ) )
            {
                return leftValue.Equals( rightValue );
            }

            return false;
        }
    }
}
