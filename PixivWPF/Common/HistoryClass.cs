using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixivWPF.Common
{
    class History<T>
    {
        private Stack<T> prev = null;
        private Stack<T> next = null;

        public History()
        {
            prev = new Stack<T>();
            next = new Stack<T>();
        }

        public T Prev
        {
            get
            {
                var h = prev.Pop();
                next.Push(h);
                return (h);
            }
        }

        public T Next
        {
            get
            {
                var h = next.Pop();
                prev.Push(h);
                return (h);
            }
            set
            {

            }
        }

    }
}
